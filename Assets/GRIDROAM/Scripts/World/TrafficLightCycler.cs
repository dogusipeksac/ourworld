using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Lightweight runtime traffic light cycler using MaterialPropertyBlock emission
    /// so mobile draw calls stay batched with zero per-instance material allocation.
    /// Exposes current signal state so AmbientTrafficManager stops cleanly at red lights.
    /// </summary>
    public class TrafficLightCycler : MonoBehaviour
    {
        [SerializeField] private Renderer redRenderer;
        [SerializeField] private Renderer amberRenderer;
        [SerializeField] private Renderer greenRenderer;
        [SerializeField] private float phaseSeconds = 8f;
        [SerializeField] private float amberSeconds = 1.5f;
        [SerializeField] private float phaseOffsetSeconds;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static readonly Color RedOn = new Color(1.0f, 0.10f, 0.08f) * 3.8f;
        private static readonly Color AmberOn = new Color(1.0f, 0.68f, 0.08f) * 3.5f;
        private static readonly Color GreenOn = new Color(0.12f, 1.0f, 0.30f) * 3.5f;
        private static readonly Color OffColor = Color.black;

        private MaterialPropertyBlock _mpb;
        private int _lastState = -1;

        public int CurrentState => _lastState;
        public bool IsGreen => _lastState == 0;
        public bool IsAmber => _lastState == 1;
        public bool IsRed => _lastState == 2;
        public bool IsRedOrAmber => _lastState == 1 || _lastState == 2;

        public void Configure(Renderer red, Renderer amber, Renderer green, float offsetSeconds = 0f)
        {
            redRenderer = red;
            amberRenderer = amber;
            greenRenderer = green;
            phaseOffsetSeconds = offsetSeconds;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            ApplyState(0);
        }

        private void Update()
        {
            float halfCycle = Mathf.Max(2f, phaseSeconds);
            float fullCycle = halfCycle * 2f;
            float t = (Time.time + phaseOffsetSeconds) % fullCycle;
            if (t < 0f) t += fullCycle;

            int state;
            if (t < halfCycle - amberSeconds)
            {
                state = 0; // Green
            }
            else if (t < halfCycle)
            {
                state = 1; // Amber
            }
            else
            {
                state = 2; // Red
            }

            if (state != _lastState)
            {
                ApplyState(state);
            }
        }

        private void ApplyState(int state)
        {
            _lastState = state;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            SetRendererEmission(greenRenderer, state == 0 ? GreenOn : OffColor);
            SetRendererEmission(amberRenderer, state == 1 ? AmberOn : OffColor);
            SetRendererEmission(redRenderer, state == 2 ? RedOn : OffColor);
        }

        private void SetRendererEmission(Renderer target, Color emission)
        {
            if (target == null) return;
            target.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorId, emission);
            target.SetPropertyBlock(_mpb);
        }
    }
}
