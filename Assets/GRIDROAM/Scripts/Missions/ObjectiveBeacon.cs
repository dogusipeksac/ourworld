using UnityEngine;

namespace Gridroam.Missions
{
    /// <summary>
    /// Lightweight 3D beacon (translucent ring + floating diamond) marking the active mission anchor.
    /// </summary>
    public class ObjectiveBeacon : MonoBehaviour
    {
        [SerializeField] private Transform floatingMarker;
        [SerializeField] private Transform groundRing;
        [SerializeField] private bool showWorldBeacon;
        [SerializeField] private float bobAmplitude = 0.35f;
        [SerializeField] private float bobSpeed = 2.6f;
        [SerializeField] private float spinSpeed = 75f;

        private Vector3 _baseFloatingLocalPos;
        private bool _visible;

        public void ConfigureParts(Transform floating, Transform ring)
        {
            floatingMarker = floating;
            groundRing = ring;
            if (floatingMarker != null)
            {
                _baseFloatingLocalPos = floatingMarker.localPosition;
            }
        }

        private void Awake()
        {
            if (floatingMarker != null)
            {
                _baseFloatingLocalPos = floatingMarker.localPosition;
            }
        }

        public void SetTarget(Vector3 worldPos, float radius)
        {
            transform.position = worldPos;
            if (groundRing != null)
            {
                float diameter = Mathf.Max(2.2f, radius * 2f);
                groundRing.localScale = new Vector3(diameter, 0.08f, diameter);
            }

            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible && showWorldBeacon;
            gameObject.SetActive(_visible);
        }

        private void Update()
        {
            if (!_visible || floatingMarker == null)
            {
                return;
            }

            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            floatingMarker.localPosition = _baseFloatingLocalPos + Vector3.up * bob;
            floatingMarker.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }
    }
}
