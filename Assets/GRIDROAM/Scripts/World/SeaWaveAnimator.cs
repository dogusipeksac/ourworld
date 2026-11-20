using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Gently animates coastal water normal/UV flow and optional floating harbor buoys
    /// with virtually zero CPU cost on Android & iOS.
    /// </summary>
    public class SeaWaveAnimator : MonoBehaviour
    {
        [SerializeField] private Renderer waterRenderer;
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.018f, 0.012f);
        [SerializeField] private Transform[] floatingProps;
        [SerializeField] private float bobHeight = 0.14f;
        [SerializeField] private float bobFrequency = 1.4f;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");

        private Material _runtimeMat;
        private Vector3[] _basePositions;
        private Vector2 _offset;

        public void Configure(Renderer rendererTarget, Transform[] buoys)
        {
            waterRenderer = rendererTarget;
            floatingProps = buoys;
        }

        private void Start()
        {
            if (waterRenderer != null)
            {
                _runtimeMat = waterRenderer.material;
            }

            if (floatingProps != null && floatingProps.Length > 0)
            {
                _basePositions = new Vector3[floatingProps.Length];
                for (int i = 0; i < floatingProps.Length; i++)
                {
                    if (floatingProps[i] != null)
                    {
                        _basePositions[i] = floatingProps[i].position;
                    }
                }
            }
        }

        private void Update()
        {
            if (_runtimeMat != null)
            {
                _offset += scrollSpeed * Time.deltaTime;
                _runtimeMat.SetTextureOffset(BaseMapId, _offset);
                if (_runtimeMat.HasProperty(BumpMapId))
                {
                    _runtimeMat.SetTextureOffset(BumpMapId, _offset * 1.35f);
                }
            }

            if (floatingProps != null && _basePositions != null)
            {
                float t = Time.time * bobFrequency;
                for (int i = 0; i < floatingProps.Length; i++)
                {
                    Transform prop = floatingProps[i];
                    if (prop == null) continue;
                    float phase = t + i * 1.7f;
                    Vector3 basePos = _basePositions[i];
                    prop.position = new Vector3(basePos.x, basePos.y + Mathf.Sin(phase) * bobHeight, basePos.z);
                    prop.localRotation = Quaternion.Euler(Mathf.Sin(phase * 0.7f) * 4.5f, prop.localEulerAngles.y, Mathf.Cos(phase * 0.8f) * 4.5f);
                }
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMat != null)
            {
                Destroy(_runtimeMat);
            }
        }
    }
}
