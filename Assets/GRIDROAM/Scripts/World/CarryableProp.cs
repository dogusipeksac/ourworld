using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Physical Grab & Throw street prop (Damacana Su, Manav Kasası, Trafik Konisi, Kargo Kolisi):
    /// - Press E ('Taşı / Bırak') to pick up and carry the object smoothly in front of the player.
    /// - Press Left-Click / V while carrying to hurl it physically across Selman Sokağı!
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarryableProp : MonoBehaviour
    {
        [SerializeField] private string propLabel = "Damacana";
        [SerializeField] private float carryDistance = 0.85f;
        [SerializeField] private float carryHeight = 1.05f;
        [SerializeField] private float throwSpeed = 11.5f;

        private static CarryableProp _currentlyHeld;

        private Rigidbody _rb;
        private Collider[] _colliders;
        private Transform _holder;
        private Vector3 _spawnPos;
        private Quaternion _spawnRot;

        public static bool IsHoldingProp => _currentlyHeld != null;
        public static CarryableProp CurrentlyHeld => _currentlyHeld;
        public string PropLabel => propLabel;

        public void Configure(string label, float throwVelocity = 11.5f)
        {
            propLabel = label;
            throwSpeed = throwVelocity;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void Start()
        {
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
        }

        public static bool TryThrowHeldProp(Vector3 forwardDir)
        {
            if (_currentlyHeld == null) return false;
            _currentlyHeld.Throw(forwardDir);
            return true;
        }

        public static void DropHeldPropIfAny()
        {
            if (_currentlyHeld != null)
            {
                _currentlyHeld.Drop(Vector3.zero);
            }
        }

        public void ToggleCarry(PlayerModeController mode)
        {
            if (mode == null || mode.FootController == null) return;

            if (_currentlyHeld == this)
            {
                Drop(mode.FootController.transform.forward * 1.4f);
                mode.RaiseStatusMessage($"{propLabel} yere bırakıldı.");
                return;
            }

            if (_currentlyHeld != null)
            {
                _currentlyHeld.Drop(Vector3.zero);
            }

            _currentlyHeld = this;
            _holder = mode.FootController.transform;
            _rb.isKinematic = true;
            _rb.useGravity = false;
            SetCollidersEnabled(false);

            GameFeelAudio.PlayDoorLatch();
            mode.RaiseStatusMessage($"{propLabel} eline alındı · Bırak: E · Fırlat: Sol Tık / V");
        }

        public void Throw(Vector3 forwardDir)
        {
            Vector3 dir = forwardDir;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 throwVel = dir * throwSpeed + Vector3.up * 3.6f;
            Drop(throwVel);
            _rb.AddTorque(UnityEngine.Random.onUnitSphere * 8f, ForceMode.VelocityChange);
            GameFeelAudio.PlayUiClick();
            GameFeelAudio.SpawnBurst(transform.position, new Color(0.90f, 0.94f, 1.0f), count: 6, spread: 0.3f);
        }

        private void Drop(Vector3 releaseVelocity)
        {
            if (_currentlyHeld == this) _currentlyHeld = null;
            _holder = null;
            SetCollidersEnabled(true);
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = releaseVelocity;
            GameFeelAudio.PlayDoorLatch();
        }

        private void SetCollidersEnabled(bool state)
        {
            if (_colliders == null) return;
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null && !_colliders[i].isTrigger)
                {
                    _colliders[i].enabled = state;
                }
            }
        }

        private void LateUpdate()
        {
            if (_currentlyHeld == this && _holder != null)
            {
                Vector3 targetPos = _holder.position + _holder.forward * carryDistance + Vector3.up * carryHeight;
                transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-20f * Time.deltaTime));
                transform.rotation = Quaternion.Slerp(transform.rotation, _holder.rotation, 1f - Mathf.Exp(-14f * Time.deltaTime));
                return;
            }

            if (transform.position.y < -2.0f)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                transform.SetPositionAndRotation(_spawnPos + Vector3.up * 0.25f, _spawnRot);
            }
        }

        private void OnDisable()
        {
            if (_currentlyHeld == this)
            {
                _currentlyHeld = null;
            }
        }
    }
}
