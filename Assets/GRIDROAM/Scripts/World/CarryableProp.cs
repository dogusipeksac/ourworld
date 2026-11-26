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
        private const float PutDownDuration = 0.38f;
        private const float ThrowWindupDuration = 0.14f;
        private const float ThrowReleaseTime = 0.22f;
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
        private float _carryHeaviness = 0.72f;
        private Vector3 _spawnPos;
        private Quaternion _spawnRot;
        private Vector3 _liftStart;
        private Quaternion _liftRotation;
        private float _pickupDeadline;
        private float _reachStarted = -1f;
        private bool _attached;
        private bool _puttingDown;
        private float _putDownStarted = -1f;
        private Vector3 _putDownStartPos;
        private Quaternion _putDownStartRot;
        private Vector3 _putDownTargetPos;
        private Quaternion _putDownTargetRot;
        private bool _throwing;
        private float _throwStarted = -1f;
        private Vector3 _throwDirection = Vector3.forward;
        private Vector3 _throwStartLocalPos;
        private Quaternion _throwStartLocalRot;

        public static bool IsHoldingProp => _currentlyHeld != null && _currentlyHeld._attached;
        public static CarryableProp CurrentlyHeld => _currentlyHeld;
        public string PropLabel => propLabel;
        internal float CarryHeaviness => _carryHeaviness;
        internal bool IsAttached => _attached;
        internal bool IsThrowing => _throwing;

        internal float ThrowProgress
        {
            get
            {
                if (!_throwing || _throwStarted < 0f) return 0f;
                return Mathf.Clamp01((Time.time - _throwStarted) / ThrowReleaseTime);
            }
        }

        internal float PickupCrouch
        {
            get
            {
                if (_puttingDown && _putDownStarted >= 0f)
                {
                    return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _putDownStarted) / PutDownDuration));
                }
                if (_throwing && _throwStarted >= 0f)
                {
                    float t = Mathf.Clamp01((Time.time - _throwStarted) / ThrowReleaseTime);
                    return Mathf.Sin(t * Mathf.PI) * 0.16f;
                }
                float elapsed = Time.time - _reachStarted;
                return Mathf.SmoothStep(0f, 1f, elapsed / ReachDuration) *
                    (1f - Mathf.SmoothStep(0f, 1f, (elapsed - ReachDuration) / LiftDuration));
            }
        }

        public void Configure(string label, float throwVelocity = 11.5f)
        {
            propLabel = label;
            throwSpeed = throwVelocity;
            ComputeHeaviness();
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
            ComputeHeaviness();
        }

        private void ComputeHeaviness()
        {
            bool isCone = transform.Find("ConeBody") != null;
            bool isDamacana = transform.Find("Damacana_Mesh") != null;
            string lbl = propLabel != null ? propLabel.ToLowerInvariant() : string.Empty;
            if (isDamacana || lbl.Contains("damacana") || lbl.Contains("bidon") || lbl.Contains("su"))
            {
                _carryHeaviness = 0.96f;
            }
            else if (isCone || lbl.Contains("koni") || lbl.Contains("duba"))
            {
                _carryHeaviness = 0.35f;
            }
            else
            {
                float massHint = _rb != null ? Mathf.InverseLerp(1.5f, 14f, _rb.mass) : 0.5f;
                float vol = _shapeExtents.x * _shapeExtents.y * _shapeExtents.z * 8f;
                float volHint = Mathf.InverseLerp(0.015f, 0.12f, vol);
                _carryHeaviness = Mathf.Clamp(Mathf.Lerp(0.42f, 0.92f, Mathf.Max(massHint, volHint)), 0.35f, 0.96f);
            }
        }

        private void Start()
        {
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
        }

        public static bool TryThrowHeldProp(Vector3 forwardDir)
        {
            if (!IsHoldingProp) return false;
            if (_currentlyHeld._throwing || _currentlyHeld._puttingDown) return true;
            _currentlyHeld.BeginThrow(forwardDir);
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
                if (_throwing) return;
                if (_puttingDown)
                {
                    Drop(Vector3.zero);
                    mode.RaiseStatusMessage(LocalizationService.IsEnglish ? $"Placed {propLabel} down." : $"{propLabel} yere bırakıldı.");
                    return;
                }
                if (_attached)
                {
                    BeginPutDown(foot);
                    return;
                }
                Drop(Vector3.zero);
                return;
            }

            DropHeldPropIfAny();
            _currentlyHeld = this;
            _mode = mode;
            _holder = foot;
            _holderCollider = foot.GetComponent<CharacterController>();
            _pickupDeadline = Time.time + 1.9f;
        }

        private void BeginPutDown(PlayerFootController foot)
        {
            _puttingDown = true;
            _putDownStarted = Time.time;
            _putDownStartPos = transform.position;
            _putDownStartRot = transform.rotation;

            Transform footTr = foot.transform;
            Vector3 fwd = footTr.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            float placeDist = (_holderCollider != null ? _holderCollider.radius : 0.32f) + _shapeExtents.z + 0.16f;
            Vector3 spot = footTr.position + fwd * placeDist;
            float groundY = footTr.position.y;
            if (Physics.Raycast(spot + Vector3.up * 0.65f, Vector3.down, out RaycastHit hit, 1.5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.transform.root != footTr.root && hit.collider.transform.root != transform.root)
            {
                groundY = hit.point.y;
            }

            float bottomOffset = (_shapeCenter.y - _shapeExtents.y) * transform.lossyScale.y;
            _putDownTargetRot = Quaternion.LookRotation(fwd, Vector3.up);
            _putDownTargetPos = new Vector3(spot.x, groundY - bottomOffset + 0.01f, spot.z);
        }

        private void BeginThrow(Vector3 forwardDir)
        {
            Vector3 dir = Vector3.ProjectOnPlane(forwardDir, Vector3.up);
            if (dir.sqrMagnitude < 0.01f) dir = _holder != null ? _holder.transform.forward : Vector3.forward;
            _throwDirection = dir.normalized;
            _throwing = true;
            _throwStarted = Time.time;
            if (_holder != null && _holder.Humanoid != null)
            {
                Transform rigRoot = _holder.Humanoid.transform;
                _throwStartLocalPos = rigRoot.InverseTransformPoint(transform.position);
                _throwStartLocalRot = Quaternion.Inverse(rigRoot.rotation) * transform.rotation;
            }
        }

        internal bool TryGetPickupMotion(PlayerFootController player, bool canContinue, Vector3 requestedDirection,
            out Vector3 direction, out float speed)
        {
            direction = Vector3.zero;
            speed = 0f;
            if (_holder != player) return false;

            if (_puttingDown)
            {
                Vector3 toPlace = Vector3.ProjectOnPlane(_putDownTargetPos - player.transform.position, Vector3.up);
                direction = toPlace.sqrMagnitude > 0.001f ? toPlace.normalized : player.transform.forward;
                if (!canContinue || requestedDirection.sqrMagnitude > 0.45f)
                {
                    PlayerModeController m = _mode;
                    Drop(Vector3.zero);
                    if (m != null) m.RaiseStatusMessage(LocalizationService.IsEnglish ? $"Placed {propLabel} down." : $"{propLabel} yere bırakıldı.");
                    return false;
                }
                return true;
            }

            if (_attached) return false;

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

            if (_puttingDown)
            {
                float putElapsed = Time.time - _putDownStarted;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(putElapsed / PutDownDuration));
                Vector3 pos = Vector3.Lerp(_putDownStartPos, _putDownTargetPos, u);
                // Subtle downward arc so prop clears thighs while squatting down
                pos += rigRoot.forward * (Mathf.Sin(u * Mathf.PI) * 0.05f);
                Quaternion rot = Quaternion.Slerp(_putDownStartRot, _putDownTargetRot, u);
                transform.SetPositionAndRotation(pos, rot);

                ComputePalmTargets(rigRoot, out left, out right);
                if (putElapsed >= PutDownDuration)
                {
                    PlayerModeController m = _mode;
                    rig.BeginCarryReleaseFade(left, right, 1f);
                    Drop(Vector3.zero);
                    if (m != null) m.RaiseStatusMessage(LocalizationService.IsEnglish ? $"Placed {propLabel} down." : $"{propLabel} yere bırakıldı.");
                    return 0f;
                }
                return 1f;
            }

            if (_throwing)
            {
                float throwElapsed = Time.time - _throwStarted;
                if (throwElapsed < ThrowWindupDuration)
                {
                    float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(throwElapsed / ThrowWindupDuration));
                    Vector3 windLocal = _throwStartLocalPos + new Vector3(0f, 0.11f, -0.13f);
                    Quaternion windRot = _throwStartLocalRot * Quaternion.Euler(-16f, 0f, 0f);
                    transform.SetPositionAndRotation(
                        rigRoot.TransformPoint(Vector3.Lerp(_throwStartLocalPos, windLocal, w)),
                        rigRoot.rotation * Quaternion.Slerp(_throwStartLocalRot, windRot, w));
                }
                else
                {
                    float h = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((throwElapsed - ThrowWindupDuration) / (ThrowReleaseTime - ThrowWindupDuration)));
                    Vector3 windLocal = _throwStartLocalPos + new Vector3(0f, 0.11f, -0.13f);
                    Vector3 releaseLocal = _throwStartLocalPos + new Vector3(0f, 0.24f, 0.30f);
                    Quaternion windRot = _throwStartLocalRot * Quaternion.Euler(-16f, 0f, 0f);
                    Quaternion releaseRot = _throwStartLocalRot * Quaternion.Euler(22f, 0f, 0f);
                    transform.SetPositionAndRotation(
                        rigRoot.TransformPoint(Vector3.Lerp(windLocal, releaseLocal, h)),
                        rigRoot.rotation * Quaternion.Slerp(windRot, releaseRot, h));
                }

                ComputePalmTargets(rigRoot, out left, out right);
                if (throwElapsed >= ThrowReleaseTime)
                {
                    float heaviness = _carryHeaviness;
                    Vector3 dir = _throwDirection;
                    rig.PlayCarryThrowFollowThrough(heaviness, left, right);
                    Throw(dir);
                    return 0f;
                }
                return 1f;
            }

            if (elapsed >= ReachDuration)
            {
                if (!_attached)
                {
                    _liftStart = rigRoot.InverseTransformPoint(transform.position);
                    _liftRotation = Quaternion.Inverse(rigRoot.rotation) * transform.rotation;
                    SetCollidersEnabled(false);
                    _attached = true;
                    GameFeelAudio.PlayDoorLatch();
                    if (_mode != null) _mode.RaiseStatusMessage(LocalizationService.IsEnglish
                        ? $"Picked up {propLabel} · Drop: E · Throw: Left Click / V"
                        : $"{propLabel} eline alındı · Bırak: E · Fırlat: Sol Tık / V");
                }

                float lift = Mathf.SmoothStep(0f, 1f, (elapsed - ReachDuration) / LiftDuration);
                float tiltBack = Mathf.Lerp(-4f, -10f, _carryHeaviness);
                Quaternion rotation = rigRoot.rotation * Quaternion.Euler(tiltBack, 0f, 0f);
                float carryY = Mathf.Lerp(0.21f, 0.16f, _carryHeaviness);
                Vector3 center = anchor + rigRoot.TransformVector(
                    new Vector3(0f, carryY, 0.205f + _shapeExtents.z));
                Vector3 position = center - rotation * Vector3.Scale(_shapeCenter, transform.lossyScale);
                transform.SetPositionAndRotation(Vector3.Lerp(rigRoot.TransformPoint(_liftStart), position, lift),
                    Quaternion.Slerp(rigRoot.rotation * _liftRotation, rotation, lift));
            }

            ComputePalmTargets(rigRoot, out left, out right);
            return Mathf.SmoothStep(0f, 1f, elapsed / ReachDuration);
        }

        private void ComputePalmTargets(Transform rigRoot, out Vector3 left, out Vector3 right)
        {
            Vector3 localRight = transform.InverseTransformDirection(rigRoot.right);
            Vector3 side;
            if (Mathf.Abs(localRight.y) > 0.82f)
            {
                side = Vector3.up * (Mathf.Sign(localRight.y) * (_shapeExtents.y * 0.5f + 0.024f));
            }
            else if (_roundGrip)
            {
                localRight.y = 0f;
                Vector3 rightFlat = localRight.sqrMagnitude > 0.0001f ? localRight.normalized : Vector3.right;
                side = rightFlat * (_gripRadius + 0.026f);
            }
            else
            {
                side = Mathf.Abs(localRight.x) >= Mathf.Abs(localRight.z)
                    ? Vector3.right * (Mathf.Sign(localRight.x) * (_shapeExtents.x + 0.024f))
                    : Vector3.forward * (Mathf.Sign(localRight.z) * (_shapeExtents.z + 0.024f));
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
            _puttingDown = false;
            _putDownStarted = -1f;
            _throwing = false;
            _throwStarted = -1f;
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
                if (_holder == null || !_holder.isActiveAndEnabled)
                {
                    Drop(Vector3.zero);
                }
                else if (_throwing && Time.time - _throwStarted > ThrowReleaseTime + 0.12f)
                {
                    Throw(_throwDirection);
                }
                else if (_puttingDown && Time.time - _putDownStarted > PutDownDuration + 0.12f)
                {
                    PlayerModeController m = _mode;
                    Drop(Vector3.zero);
                    if (m != null) m.RaiseStatusMessage(LocalizationService.IsEnglish ? $"Placed {propLabel} down." : $"{propLabel} yere bırakıldı.");
                }
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
