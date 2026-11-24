using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Marker & lightweight controller toggled by DayNightCycle:
    /// enables exterior/street/plaza point lights and emissive window/bulb renderers
    /// at evening/night (18:30-06:30) and disables them during daytime using MaterialPropertyBlock.
    /// Automatically enables URP _EMISSION keywords and calibrates legacy streetlamp intensities
    /// for crisp, readable URP Linear street illumination.
    /// </summary>
    public class NightLight : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Light targetLight;
        [SerializeField] private Renderer[] emissiveRenderers;
        [SerializeField] private Color nightEmissionColor = new Color(1.0f, 0.80f, 0.44f, 1f) * 2.4f;
        [SerializeField] private bool overrideBaseColor;
        [SerializeField] private Color dayBaseColor = new Color(0.09f, 0.13f, 0.18f, 1f);
        [SerializeField] private Color nightBaseColor = new Color(1.0f, 0.83f, 0.54f, 1f);

        private MaterialPropertyBlock _mpb;
        private bool _appliedOnce;
        private bool _currentNightState;
        private bool _distanceCulled;
        private bool _calibrated;

        public Light TargetLight => targetLight;
        public bool IsNightActive => _currentNightState;
        public bool IsManagedOutdoorLight { get; private set; }

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
            _calibrated = false;
            CalibrateIfNeeded();
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
            CalibrateIfNeeded();
        }

        private void CalibrateIfNeeded()
        {
            if (_calibrated) return;
            _calibrated = true;

            if (targetLight != null && targetLight.type == LightType.Point && targetLight.range >= 12f)
            {
                bool isInterior = GetComponentInParent<InteriorZone>() != null;
                if (!isInterior)
                {
                    IsManagedOutdoorLight = true;
                    // Calibrate legacy gamma-era outdoor point intensities (2.4..3.8) into crisp URP Linear street pools
                    if (targetLight.intensity < 18f)
                    {
                        targetLight.intensity = Mathf.Clamp(targetLight.intensity * 12.5f, 34f, 50f);
                    }
                    if (targetLight.range < 36f)
                    {
                        targetLight.range = 36f;
                    }
                    targetLight.shadows = LightShadows.None;
                }
            }
        }

        private void OnEnable()
        {
            CalibrateIfNeeded();
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

        public void SetDistanceCulled(bool culled)
        {
            if (_distanceCulled == culled) return;
            _distanceCulled = culled;
            if (targetLight != null)
            {
                targetLight.enabled = _currentNightState && !_distanceCulled;
            }
        }

        public void ApplyNightState(bool isNight, bool force = false)
        {
            CalibrateIfNeeded();

            if (_appliedOnce && !force && _currentNightState == isNight)
            {
                return;
            }

            _appliedOnce = true;
            _currentNightState = isNight;

            if (targetLight != null)
            {
                targetLight.enabled = isNight && !_distanceCulled;
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

                    // Ensure URP Lit materials have _EMISSION keyword enabled so MPB _EmissionColor renders!
                    if (isNight)
                    {
                        Material[] mats = r.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++)
                        {
                            if (mats[m] != null && !mats[m].IsKeywordEnabled("_EMISSION"))
                            {
                                mats[m].EnableKeyword("_EMISSION");
                            }
                        }
                    }

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
