using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Gently animates the pool water normal-map UV offset and subtle vertical swell
    /// so the neighborhood swimming pool surface shimmers naturally at zero GC cost.
    /// </summary>
    public sealed class PoolWaterAnimator : MonoBehaviour
    {
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.035f, 0.022f);
        [SerializeField] private float swellAmplitude = 0.012f;
        [SerializeField] private float swellFrequency = 1.6f;

        private Renderer _renderer;
        private Material _runtimeMat;
        private Vector3 _baseLocalPos;
        private Vector2 _offset;

        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _baseLocalPos = transform.localPosition;
            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                _runtimeMat = _renderer.material;
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMat != null)
            {
                Destroy(_runtimeMat);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_runtimeMat != null && _runtimeMat.HasProperty(BumpMapId))
            {
                _offset.x = Mathf.Repeat(_offset.x + scrollSpeed.x * dt, 10f);
                _offset.y = Mathf.Repeat(_offset.y + scrollSpeed.y * dt, 10f);
                _runtimeMat.SetTextureOffset(BumpMapId, _offset);
            }

            float y = Mathf.Sin(Time.time * swellFrequency) * swellAmplitude;
            transform.localPosition = new Vector3(_baseLocalPos.x, _baseLocalPos.y + y, _baseLocalPos.z);
        }
    }
}
