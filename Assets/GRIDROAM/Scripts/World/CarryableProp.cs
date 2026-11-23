using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarryableProp : MonoBehaviour
    {
        [SerializeField] private string propLabel = "Damacana";
        [SerializeField] private float throwSpeed = 11.5f;

        private const float ReachDuration = 0.36f;
        private const float LiftDuration = 0.34f;
        private static CarryableProp _currentlyHeld;

        private Rigidbody _rb;
        private Collider[] _colliders;
        private CharacterController _holderCollider;
        private CharacterController _releaseCollider;
        private PlayerModeController _mode;
        private PlayerFootController _holder;
        private Vector3 _shapeCenter;
        private Vector3 _shapeExtents;
        private float _gripRadius;
        private bool _roundGrip;
        private Vector3 _spawnPos;
        private Quaternion _spawnRot;
        private Vector3 _liftStart;
        private Quaternion _liftRotation;
        private float _pickupDeadline;
        private float _reachStarted = -1f;
        private bool _attached;

        public static bool IsHoldingProp => _currentlyHeld != null && _currentlyHeld._attached;
        public static CarryableProp CurrentlyHeld => _currentlyHeld;
        public string PropLabel => propLabel;

        internal float PickupCrouch
        {
            get
            {
                float elapsed = Time.time - _reachStarted;
                return Mathf.SmoothStep(0f, 1f, elapsed / ReachDuration) *
                    (1f - Mathf.SmoothStep(0f, 1f, (elapsed - ReachDuration) / LiftDuration));
            }
        }

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

            BoxCollider shape = GetComponent<BoxCollider>();
            _shapeCenter = shape != null ? shape.center : Vector3.up * 0.24f;
            _shapeExtents = shape != null ? shape.size * 0.5f : new Vector3(0.15f, 0.24f, 0.15f);
            Transform cone = transform.Find("ConeBody");
            _gripRadius = cone != null ? cone.localScale.x * 0.5f : _shapeExtents.x;
            _roundGrip = cone != null || transform.Find("Damacana_Mesh") != null;
        }

        private void Start()
        {
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
        }

        public static bool TryThrowHeldProp(Vector3 forwardDir)
        {
            if (!IsHoldingProp) return false;
            _currentlyHeld.Throw(forwardDir);
            return true;
        }

        public static void DropHeldPropIfAny()
        {
            if (_currentlyHeld != null) _currentlyHeld.Drop(Vector3.zero);
        }

        public void ToggleCarry(PlayerModeController mode)
        {
            PlayerFootController foot = mode != null ? mode.FootController : null;
            if (foot == null || foot.Humanoid == null || foot.IsSwimming) return;

            if (_currentlyHeld == this)
            {
                bool wasHeld = _attached;
                Drop(foot.transform.forward * 1.4f);
                if (wasHeld) mode.RaiseStatusMessage($"{propLabel} yere bırakıldı.");
                return;
            }

            DropHeldPropIfAny();
            _currentlyHeld = this;
            _mode = mode;
            _holder = foot;
            _holderCollider = foot.GetComponent<CharacterController>();
            _pickupDeadline = Time.time + 1.9f;
        }

        internal bool TryGetPickupMotion(PlayerFootController player, bool canContinue, Vector3 requestedDirection,
            out Vector3 direction, out float speed)
        {
            direction = Vector3.zero;
            speed = 0f;
            if (_holder != player || _attached) return false;

            Vector3 toProp = Vector3.ProjectOnPlane(transform.TransformPoint(_shapeCenter) - player.transform.position, Vector3.up);
            direction = toProp.normalized;
            bool steeringAway = requestedDirection.sqrMagnitude > 0.12f &&
                Vector3.Dot(requestedDirection.normalized, direction) < 0.35f;
            if (!canContinue || steeringAway || Time.time > _pickupDeadline)
            {
                Drop(Vector3.zero);
                return false;
            }

            if (_reachStarted >= 0f) return true;

            Vector3 localDirection = transform.InverseTransformDirection(direction);
            Vector3 extents = Vector3.Scale(_shapeExtents, transform.lossyScale);
            float propDepth = Mathf.Abs(localDirection.x) * extents.x +
                Mathf.Abs(localDirection.y) * extents.y + Mathf.Abs(localDirection.z) * extents.z;
            float reachDistance = _holderCollider.radius + propDepth + 0.043f;
            speed = Mathf.Clamp((toProp.magnitude - reachDistance + 0.035f) * 5f, 0f, 2.15f);
            if (toProp.magnitude <= reachDistance && player.CurrentSpeed < 0.28f)
            {
                speed = 0f;
                _reachStarted = Time.time;
                // Pause the object while the palms close around it, then lift from this exact pose.
                SetKinematic();
            }
            return true;
        }

        internal bool UsesRig(ProceduralHumanoid rig)
        {
            return _holder != null && _holder.Humanoid == rig && _reachStarted >= 0f;
        }

        internal float UpdateGrip(ProceduralHumanoid rig, Vector3 anchor, out Vector3 left, out Vector3 right)
        {
            Transform rigRoot = rig.transform;
            float elapsed = Time.time - _reachStarted;
            if (elapsed >= ReachDuration)
            {
                if (!_attached)
                {
                    _liftStart = rigRoot.InverseTransformPoint(transform.position);
                    _liftRotation = Quaternion.Inverse(rigRoot.rotation) * transform.rotation;
                    SetCollidersEnabled(false);
                    _attached = true;
                    GameFeelAudio.PlayDoorLatch();
                    if (_mode != null) _mode.RaiseStatusMessage($"{propLabel} eline alındı · Bırak: E · Fırlat: Sol Tık / V");
                }

                float lift = Mathf.SmoothStep(0f, 1f, (elapsed - ReachDuration) / LiftDuration);
                Quaternion rotation = rigRoot.rotation * Quaternion.Euler(-7f, 0f, 0f);
                Vector3 center = anchor + rigRoot.TransformVector(
                    new Vector3(0f, 0.19f, 0.205f + _shapeExtents.z));
                Vector3 position = center - rotation * Vector3.Scale(_shapeCenter, transform.lossyScale);
                transform.SetPositionAndRotation(Vector3.Lerp(rigRoot.TransformPoint(_liftStart), position, lift),
                    Quaternion.Slerp(rigRoot.rotation * _liftRotation, rotation, lift));
            }

            Vector3 localRight = transform.InverseTransformDirection(rigRoot.right);
            Vector3 side;
            if (Mathf.Abs(localRight.y) > 0.82f)
            {
                side = Vector3.up * (Mathf.Sign(localRight.y) * _shapeExtents.y * 0.5f);
            }
            else if (_roundGrip)
            {
                localRight.y = 0f;
                side = localRight.normalized * (_gripRadius + 0.003f);
            }
            else
            {
                side = Mathf.Abs(localRight.x) >= Mathf.Abs(localRight.z)
                    ? Vector3.right * (Mathf.Sign(localRight.x) * _shapeExtents.x)
                    : Vector3.forward * (Mathf.Sign(localRight.z) * _shapeExtents.z);
            }
            Vector3 nearSide = Vector3.Cross(Vector3.up, side.normalized);
            if (nearSide.sqrMagnitude < 0.001f)
            {
                nearSide = Vector3.ProjectOnPlane(transform.InverseTransformDirection(-rigRoot.forward), side).normalized;
            }
            Vector3 gripCenter = _shapeCenter + nearSide * (_shapeExtents.z * 0.23f) -
                Vector3.up * (_shapeExtents.y * 0.17f);
            left = transform.TransformPoint(gripCenter - side);
            right = transform.TransformPoint(gripCenter + side);
            return Mathf.SmoothStep(0f, 1f, elapsed / ReachDuration);
        }

        public void Throw(Vector3 forwardDir)
        {
            if (!_attached) return;
            Vector3 dir = Vector3.ProjectOnPlane(forwardDir, Vector3.up);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;

            Drop(dir.normalized * throwSpeed + Vector3.up * 3.6f);
            _rb.AddTorque(Random.onUnitSphere * 8f, ForceMode.VelocityChange);
            GameFeelAudio.PlayUiClick();
            GameFeelAudio.SpawnBurst(transform.position, new Color(0.90f, 0.94f, 1.0f), count: 6, spread: 0.3f);
        }

        private void SetKinematic()
        {
            _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _rb.interpolation = RigidbodyInterpolation.None;
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        private void Drop(Vector3 releaseVelocity)
        {
            bool wasHeld = _attached;
            if (_reachStarted >= 0f)
            {
                _rb.position = transform.position;
                _rb.rotation = transform.rotation;
                SetCollidersEnabled(true);
                if (wasHeld && _holderCollider != null && _holderCollider.enabled)
                {
                    _releaseCollider = _holderCollider;
                    IgnoreReleaseCollision(true);
                }
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _rb.linearVelocity = wasHeld ? releaseVelocity : Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            if (_currentlyHeld == this) _currentlyHeld = null;
            _holder = null;
            _holderCollider = null;
            _mode = null;
            _reachStarted = -1f;
            _attached = false;
            if (wasHeld && isActiveAndEnabled) GameFeelAudio.PlayDoorLatch();
        }

        private void SetCollidersEnabled(bool state)
        {
            foreach (Collider shape in _colliders)
            {
                if (shape != null && !shape.isTrigger) shape.enabled = state;
            }
        }

        private void IgnoreReleaseCollision(bool ignore)
        {
            if (!_releaseCollider.enabled || !_releaseCollider.gameObject.activeInHierarchy) return;
            foreach (Collider shape in _colliders)
            {
                if (shape != null && !shape.isTrigger && shape.enabled && shape.gameObject.activeInHierarchy)
                {
                    Physics.IgnoreCollision(shape, _releaseCollider, ignore);
                }
            }
        }

        private void LateUpdate()
        {
            if (_releaseCollider != null)
            {
                bool overlaps = false;
                foreach (Collider shape in _colliders)
                {
                    if (shape != null && !shape.isTrigger && shape.bounds.Intersects(_releaseCollider.bounds))
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps || _currentlyHeld == this)
                {
                    IgnoreReleaseCollision(false);
                    _releaseCollider = null;
                }
            }

            if (_currentlyHeld == this)
            {
                if (_holder == null || !_holder.isActiveAndEnabled) Drop(Vector3.zero);
                return;
            }

            if (transform.position.y < -2.0f)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.position = _spawnPos + Vector3.up * 0.25f;
                _rb.rotation = _spawnRot;
            }
        }

        private void OnDisable()
        {
            if (_currentlyHeld == this) Drop(Vector3.zero);
            if (_releaseCollider != null)
            {
                IgnoreReleaseCollision(false);
                _releaseCollider = null;
            }
        }
    }
}
