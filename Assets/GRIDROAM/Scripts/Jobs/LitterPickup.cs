using System.Collections;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.Jobs
{
    /// <summary>
    /// Collectible street litter item (crumpled box/can) spawned during the Street Cleanup job.
    /// Walking over it on foot collects it with a short pop scale animation.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class LitterPickup : MonoBehaviour
    {
        private JobManager _owner;
        private bool _collected;
        private Vector3 _basePos;
        private Vector3 _baseScale;
        private float _phaseOffset;

        public bool IsCollected => _collected;

        public void Initialize(JobManager manager)
        {
            _owner = manager;
            _basePos = transform.position;
            _baseScale = transform.localScale;
            _phaseOffset = Random.Range(0f, Mathf.PI * 2f);

            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                sc.radius = 1.15f;
            }
        }

        private void Update()
        {
            if (_collected) return;

            float bob = Mathf.Sin(Time.time * 3.0f + _phaseOffset) * 0.045f;
            transform.position = _basePos + Vector3.up * (0.05f + bob);
            transform.Rotate(Vector3.up, 42f * Time.deltaTime, Space.World);

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null && mode.CurrentMode == ControlMode.OnFoot && mode.FootController != null)
            {
                Vector3 diff = mode.FootController.transform.position - _basePos;
                diff.y = 0f;
                if (diff.sqrMagnitude <= 1.85f)
                {
                    Collect();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected || other == null) return;
            PlayerFootController foot = other.GetComponentInParent<PlayerFootController>();
            if (foot != null && PlayerModeController.Instance != null && PlayerModeController.Instance.CurrentMode == ControlMode.OnFoot)
            {
                Collect();
            }
        }

        private void Collect()
        {
            if (_collected) return;
            _collected = true;

            if (_owner != null)
            {
                _owner.NotifyLitterCollected(this);
            }

            StartCoroutine(PopAndDestroyRoutine());
        }

        private IEnumerator PopAndDestroyRoutine()
        {
            const float duration = 0.16f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scaleFactor = t < 0.45f
                    ? Mathf.Lerp(1f, 1.42f, t / 0.45f)
                    : Mathf.Lerp(1.42f, 0f, (t - 0.45f) / 0.55f);
                transform.localScale = _baseScale * scaleFactor;
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
