using System.Collections.Generic;
using OurWorld.Input;
using UnityEngine;

namespace OurWorld.CameraSystem
{
    /// <summary>
    /// Smooth third-person camera supporting On-Foot, Seated, Indoor, and Driving modes,
    /// touch/mouse orbit, gentle driving auto-align, indoor max-distance pull-in, and
    /// multi-hit SphereCast wall collision so the camera never clips through walls.
    /// </summary>
    public class ThirdPersonCameraRig : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Transform target;

        [Header("On-Foot & Seated Profiles")]
        [SerializeField] private Vector3 footPivotOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField] private Vector3 seatedPivotOffset = new Vector3(0f, 0.95f, 0f);
        [SerializeField] private float footDistance = 4.6f;
        [SerializeField] private float footMinPitch = -18f;
        [SerializeField] private float footMaxPitch = 58f;

        [Header("Driving Profile")]
        [SerializeField] private Vector3 drivingPivotOffset = new Vector3(0f, 1.85f, 0f);
        [SerializeField] private float drivingDistance = 7.4f;
        [SerializeField] private float drivingSpeedExtraDistance = 1.35f;
        [SerializeField] private float drivingDefaultPitch = 16f;
        [SerializeField] private float drivingAutoAlignDelay = 1.4f;
        [SerializeField] private float drivingAutoAlignSpeed = 3.2f;
        [SerializeField] private float baseFov = 58f;
        [SerializeField] private float drivingMaxFov = 68f;
        [SerializeField] private float fovMaxSpeedKmh = 120f;
        [SerializeField] private float fovSmoothSpeed = 5.5f;

        [Header("Smoothing & Collision")]
        [SerializeField] private float pivotFollowSpeed = 14f;
        [SerializeField] private float distanceBlendSpeed = 6f;
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private float minCollisionDistance = 0.45f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private readonly RaycastHit[] _sphereHits = new RaycastHit[16];
        private readonly Dictionary<Object, float> _indoorZones = new Dictionary<Object, float>();

        private Camera _camera;
        private Rigidbody _targetRb;
        private float _yaw;
        private float _pitch = 16f;
        private float _currentDistance;
        private Vector3 _smoothedPivot;
        private bool _isDrivingMode;
        private bool _isSeatedMode;
        private bool _sportCameraActive;
        private float _sportCameraDistance = 7.8f;
        private float _sportCameraPitch = 24f;
        private float _sportCameraFov = 62f;
        private float? _sportLockedYaw;
        private float _timeSinceManualLook = 99f;

        public void SetSportCameraOverride(bool active, float distance = 7.8f, float pitch = 24f, float fov = 62f, float? lockYaw = null)
        {
            _sportCameraActive = active;
            _sportCameraDistance = distance;
            _sportCameraPitch = pitch;
            _sportCameraFov = fov;
            _sportLockedYaw = lockYaw;
            if (active)
            {
                _pitch = pitch;
                if (lockYaw.HasValue) _yaw = lockYaw.Value;
                _timeSinceManualLook = 99f;
            }
        }

        public float Yaw => _yaw;
        public Vector3 FlatForward => Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
        public Vector3 FlatRight => Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
        public bool IsIndoors => _indoorZones.Count > 0;
        public bool ExternalCameraControl { get; set; }

        public void SnapBehindTargetImmediate()
        {
            if (target == null) return;
            _yaw = target.eulerAngles.y;
            _pitch = _isDrivingMode ? drivingDefaultPitch : 14f;
            Vector3 offset = _isDrivingMode ? drivingPivotOffset : (_isSeatedMode ? seatedPivotOffset : footPivotOffset);
            _smoothedPivot = target.position + offset;
            _currentDistance = _isDrivingMode ? drivingDistance : GetEffectiveFootMaxDistance();
        }

        public bool TryGetGameplayPose(out Vector3 position, out Quaternion rotation, out float fov)
        {
            position = transform.position;
            rotation = transform.rotation;
            fov = _camera != null ? _camera.fieldOfView : baseFov;
            if (target == null) return false;

            Vector3 offset = _isDrivingMode ? drivingPivotOffset : (_isSeatedMode ? seatedPivotOffset : footPivotOffset);
            Vector3 pivot = target.position + offset;
            float dist = _sportCameraActive
                ? _sportCameraDistance
                : (_isDrivingMode ? drivingDistance : GetEffectiveFootMaxDistance());
            float yaw = (_sportCameraActive && _sportLockedYaw.HasValue) ? _sportLockedYaw.Value : _yaw;
            float pitch = _sportCameraActive
                ? _sportCameraPitch
                : Mathf.Clamp(_pitch, _isDrivingMode ? 4f : footMinPitch, _isDrivingMode ? 42f : footMaxPitch);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 backDir = rot * Vector3.back;
            float safeDist = ComputeCollisionAdjustedDistance(pivot, backDir, dist);
            position = pivot + backDir * safeDist;
            if (!_isDrivingMode)
            {
                float minCamY = target.position.y + 0.55f;
                if (position.y < minCamY) position.y = minCamY;
            }
            rotation = Quaternion.LookRotation((pivot - position).normalized, Vector3.up);

            if (_camera == null) _camera = GetComponent<Camera>();
            float aspect = (_camera != null && _camera.aspect > 0.1f) ? _camera.aspect : (16f / 9f);
            float tabletFovBoost = aspect < 1.70f ? Mathf.Clamp((1.70f - aspect) * 26f, 0f, 12f) : 0f;
            fov = baseFov + tabletFovBoost;
            return true;
        }

        private void Start()
        {
            _camera = GetComponent<Camera>();
            _currentDistance = footDistance;
            if (target != null)
            {
                _targetRb = target.GetComponent<Rigidbody>();
                _yaw = target.eulerAngles.y;
                _smoothedPivot = target.position + footPivotOffset;
            }
        }

        public void SetTarget(Transform newTarget, bool drivingMode, bool snapBehindTarget = false)
        {
            target = newTarget;
            _isDrivingMode = drivingMode;
            if (drivingMode)
            {
                _isSeatedMode = false;
            }
            _targetRb = target != null ? target.GetComponent<Rigidbody>() : null;

            if (target != null && snapBehindTarget)
            {
                _yaw = (_sportCameraActive && _sportLockedYaw.HasValue) ? _sportLockedYaw.Value : target.eulerAngles.y;
                _pitch = _sportCameraActive ? _sportCameraPitch : (drivingMode ? drivingDefaultPitch : 14f);
                Vector3 offset = drivingMode ? drivingPivotOffset : (_isSeatedMode ? seatedPivotOffset : footPivotOffset);
                _smoothedPivot = target.position + offset;
                _currentDistance = _sportCameraActive
                    ? _sportCameraDistance
                    : (drivingMode ? drivingDistance : GetEffectiveFootMaxDistance());
            }
        }

        public void SetSeatedMode(bool seated)
        {
            _isSeatedMode = seated;
        }

        public void SetIndoorZone(Object zoneKey, bool inside, float maxDistance = 2.2f)
        {
            if (zoneKey == null) return;
            if (inside)
            {
                _indoorZones[zoneKey] = Mathf.Max(1.2f, maxDistance);
            }
            else
            {
                _indoorZones.Remove(zoneKey);
            }
        }

        private float GetEffectiveFootMaxDistance()
        {
            float dist = footDistance;
            if (_indoorZones.Count > 0)
            {
                foreach (KeyValuePair<Object, float> kvp in _indoorZones)
                {
                    if (kvp.Key != null && kvp.Value < dist)
                    {
                        dist = kvp.Value;
                    }
                }
            }
            return dist;
        }

        private void LateUpdate()
        {
            if (ExternalCameraControl || target == null)
            {
                return;
            }

            InputHub input = InputHub.Instance;
            if (input != null)
            {
                Vector2 look = input.LookDelta;
                if (look.sqrMagnitude > 0.00001f)
                {
                    _yaw += look.x;
                    _pitch -= look.y;
                    _timeSinceManualLook = 0f;
                }
                else
                {
                    _timeSinceManualLook += Time.deltaTime;
                }
            }

            if (_sportCameraActive && _sportLockedYaw.HasValue)
            {
                _yaw = Mathf.LerpAngle(_yaw, _sportLockedYaw.Value, 1f - Mathf.Exp(-5.5f * Time.deltaTime));
                _pitch = Mathf.Lerp(_pitch, _sportCameraPitch, 1f - Mathf.Exp(-4.5f * Time.deltaTime));
            }
            else if (_sportCameraActive && _timeSinceManualLook > 1.8f)
            {
                _pitch = Mathf.Lerp(_pitch, _sportCameraPitch, 1f - Mathf.Exp(-3.0f * Time.deltaTime));
            }
            else if (_isDrivingMode && _timeSinceManualLook > drivingAutoAlignDelay)
            {
                float targetYaw = target.eulerAngles.y;
                _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-drivingAutoAlignSpeed * Time.deltaTime));
                _pitch = Mathf.Lerp(_pitch, drivingDefaultPitch, 1f - Mathf.Exp(-2.2f * Time.deltaTime));
            }

            Vector3 desiredOffset = _isDrivingMode
                ? drivingPivotOffset
                : (_isSeatedMode ? seatedPivotOffset : footPivotOffset);
            Vector3 desiredPivot = target.position + desiredOffset;
            _smoothedPivot = Vector3.Lerp(_smoothedPivot, desiredPivot, 1f - Mathf.Exp(-pivotFollowSpeed * Time.deltaTime));

            // Automatic overhead ceiling detection: works inside ANY house, shop, porch or garage
            // even before an InteriorZone trigger fires so the camera can NEVER float above a roof!
            bool hasOverheadCeiling = false;
            float ceilingY = float.MaxValue;
            if (!_isDrivingMode &&
                Physics.Raycast(_smoothedPivot, Vector3.up, out RaycastHit ceilHit, 4.2f, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (ceilHit.transform != null && ceilHit.transform != target && !ceilHit.transform.IsChildOf(target))
                {
                    hasOverheadCeiling = true;
                    ceilingY = ceilHit.point.y;
                }
            }

            bool effectivelyIndoors = !_isDrivingMode && (IsIndoors || hasOverheadCeiling);

            float minPitch = _isDrivingMode ? 4f : (hasOverheadCeiling ? -10f : footMinPitch);
            float maxPitch = _isDrivingMode ? 42f : (hasOverheadCeiling ? 22f : footMaxPitch);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            float speedFactor = 0f;
            if (_isDrivingMode && _targetRb != null)
            {
                float speedKmh = _targetRb.linearVelocity.magnitude * 3.6f;
                speedFactor = Mathf.InverseLerp(0f, fovMaxSpeedKmh, speedKmh);
            }

            float desiredDistance = _sportCameraActive
                ? _sportCameraDistance
                : (_isDrivingMode
                    ? drivingDistance + speedFactor * drivingSpeedExtraDistance
                    : (hasOverheadCeiling ? Mathf.Min(GetEffectiveFootMaxDistance(), 1.90f) : GetEffectiveFootMaxDistance()));
            float distSpeed = effectivelyIndoors ? distanceBlendSpeed * 2.2f : distanceBlendSpeed;
            _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, 1f - Mathf.Exp(-distSpeed * Time.deltaTime));

            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                float baseTargetFov = _sportCameraActive
                    ? _sportCameraFov
                    : (_isDrivingMode
                        ? Mathf.Lerp(baseFov, drivingMaxFov, speedFactor)
                        : (effectivelyIndoors ? 66f : baseFov));
                // Preserve wide horizontal view on 4:3 iPads, 16:10 Android tablets & foldables
                float aspect = _camera.aspect > 0.1f ? _camera.aspect : (16f / 9f);
                float tabletFovBoost = aspect < 1.70f ? Mathf.Clamp((1.70f - aspect) * 26f, 0f, 12f) : 0f;
                float desiredFov = baseTargetFov + tabletFovBoost;
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, desiredFov, 1f - Mathf.Exp(-fovSmoothSpeed * Time.deltaTime));
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 backDir = rotation * Vector3.back;

            float finalDistance = ComputeCollisionAdjustedDistance(_smoothedPivot, backDir, _currentDistance);

            Vector3 shakeOffset = Vector3.zero;
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.unscaledDeltaTime;
                float amp = _shakeIntensity * Mathf.Clamp01(_shakeTimer / Mathf.Max(0.01f, _shakeDuration));
                shakeOffset = new Vector3(
                    (Mathf.PerlinNoise(Time.unscaledTime * 42f, 1.3f) - 0.5f) * 2f * amp,
                    (Mathf.PerlinNoise(2.7f, Time.unscaledTime * 42f) - 0.5f) * 2f * amp,
                    0f);
            }

            Vector3 finalPos = _smoothedPivot + backDir * finalDistance + transform.TransformDirection(shakeOffset);

            // Hard safety clamp below overhead ceiling & above floor so camera NEVER clips through roof slabs!
            if (hasOverheadCeiling)
            {
                float maxCamY = ceilingY - 0.36f;
                if (finalPos.y > maxCamY)
                {
                    finalPos.y = maxCamY;
                }
            }
            if (!_isDrivingMode)
            {
                float minCamY = target.position.y + 0.55f;
                if (finalPos.y < minCamY)
                {
                    finalPos.y = minCamY;
                }
            }

            // Pinpoint secondary linecast guarantees camera stays cleanly inside walls even near tight doorway lintels
            if (Physics.Linecast(_smoothedPivot, finalPos, out RaycastHit lineHit, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (lineHit.transform != null && lineHit.transform != target && !lineHit.transform.IsChildOf(target))
                {
                    Vector3 toCam = finalPos - _smoothedPivot;
                    float safeDist = Mathf.Max(0.32f, lineHit.distance - 0.16f);
                    if (toCam.sqrMagnitude > 0.0001f)
                    {
                        finalPos = _smoothedPivot + toCam.normalized * safeDist;
                    }
                }
            }

            transform.position = finalPos;
            transform.LookAt(_smoothedPivot + shakeOffset * 0.45f);
        }

        private float _shakeTimer;
        private float _shakeDuration;
        private float _shakeIntensity;

        public void AddShake(float intensity, float duration = 0.18f)
        {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
            _shakeDuration = Mathf.Max(0.05f, duration);
            _shakeTimer = _shakeDuration;
        }

        private float ComputeCollisionAdjustedDistance(Vector3 origin, Vector3 direction, float maxDist)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                collisionRadius,
                direction,
                _sphereHits,
                maxDist,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            float closest = maxDist;
            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTr = _sphereHits[i].transform;
                if (hitTr == null || hitTr == target || hitTr.IsChildOf(target))
                {
                    continue;
                }

                float d = _sphereHits[i].distance;
                if (d < closest)
                {
                    closest = d;
                }
            }

            if (closest < maxDist)
            {
                float minClamp = _isDrivingMode ? 1.25f : minCollisionDistance;
                return Mathf.Max(minClamp, closest - 0.12f);
            }

            return maxDist;
        }
    }
}
