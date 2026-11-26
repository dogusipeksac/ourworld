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
        private float _smoothedCollisionDist;
        private bool _collisionDistInit;
        private Vector3 _smoothedPivot;
        private bool _isDrivingMode;
        private bool _isSeatedMode;
        private bool _sportCameraActive;
        private float _sportCameraDistance = 7.8f;
        private float _sportCameraPitch = 24f;
        private float _sportCameraFov = 62f;
        private float? _sportLockedYaw;
        private float _timeSinceManualLook = 99f;

        // Dynamic game-feel channels (sprint FOV/pullback, landing spring dip, nitro/shift FOV impulse,
        // organic grounded stride bob, driving G-force bank & turn look-ahead, surface vibration & underwater sway)
        private float _sprintMotion01;
        private float _smoothedSprint01;
        private bool _nitroBoostActive;
        private float _nitroBoostBlend;
        private float _fovImpulse;
        private float _landingDipY;
        private float _landingDipVel;
        private float _footStrideSpeed;
        private float _stridePhase;
        private bool _underwaterActive;
        private float _driveCornerG;
        private float _smoothedDriveRollDeg;
        private float _smoothedCornerLookAheadYaw;
        private float _surfaceVibAmp;
        private float _surfaceVibDecay;

        public void SetSprintMotion(float sprint01)
        {
            _sprintMotion01 = Mathf.Clamp01(sprint01);
        }

        public void SetFootStrideMotion(float footSpeedMs, bool isUnderwater)
        {
            _footStrideSpeed = Mathf.Max(0f, footSpeedMs);
            _underwaterActive = isUnderwater;
        }

        public void SetDrivingDynamics(float lateralCornerG, float roadRoughness01)
        {
            _driveCornerG = Mathf.Clamp(lateralCornerG, -1.5f, 1.5f);
            _surfaceVibAmp = Mathf.Max(_surfaceVibAmp, Mathf.Clamp01(roadRoughness01) * 0.022f);
            _surfaceVibDecay = 0.14f;
        }

        public void SetNitroBoost(bool active)
        {
            _nitroBoostActive = active;
        }

        public void AddFovImpulse(float degrees)
        {
            _fovImpulse = Mathf.Clamp(_fovImpulse + degrees, -4.5f, 8.5f);
        }

        public void AddLandingDip(float strength01)
        {
            float clamped = Mathf.Clamp01(strength01);
            float kick = Mathf.Lerp(-1.35f, -3.45f, clamped);
            _landingDipVel = Mathf.Min(_landingDipVel, kick);
        }

        // Over-the-shoulder TPS Aiming / ADS / Sniper Scope profile (Shooting Range)
        private bool _aimCameraActive;
        private Vector3 _aimShoulderOffset = new Vector3(0.48f, 1.56f, 0f);
        private float _aimDistance = 2.15f;
        private float _aimFov = 50f;
        private float _aimSensScale = 0.72f;
        private float _aimMinPitch = -32f;
        private float _aimMaxPitch = 42f;
        private float _aimYawCenter = 180f;
        private float _aimYawMaxHalfArc = 64f;
        private float _aimSwayPitch;
        private float _aimSwayYaw;

        public void SetAimCameraOverride(
            bool active,
            Vector3 shoulderOffset,
            float distance,
            float fov,
            float sensScale = 0.72f,
            float minPitch = -32f,
            float maxPitch = 42f,
            float yawCenterDeg = 180f,
            float yawMaxHalfArcDeg = 64f)
        {
            _aimCameraActive = active;
            _aimShoulderOffset = shoulderOffset;
            _aimDistance = distance;
            _aimFov = fov;
            _aimSensScale = Mathf.Clamp(sensScale, 0.08f, 1.5f);
            _aimMinPitch = minPitch;
            _aimMaxPitch = maxPitch;
            _aimYawCenter = yawCenterDeg;
            _aimYawMaxHalfArc = Mathf.Clamp(yawMaxHalfArcDeg, 10f, 180f);
            if (!active)
            {
                _aimSwayPitch = 0f;
                _aimSwayYaw = 0f;
            }
        }

        public void SetAimAngles(float yawDeg, float pitchDeg)
        {
            _yaw = yawDeg;
            _pitch = pitchDeg;
            SnapAimPivotImmediate();
        }

        public void SnapAimPivotImmediate()
        {
            if (target == null) return;
            Vector3 aimRight = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            _smoothedPivot = target.position
                + Vector3.up * _aimShoulderOffset.y
                + aimRight * _aimShoulderOffset.x;
            _currentDistance = _aimDistance;
        }

        public void AddAimDelta(float yawDeltaDeg, float pitchDeltaDeg)
        {
            _yaw += yawDeltaDeg;
            _pitch += pitchDeltaDeg;
        }

        public void ApplyAimKick(float pitchUpDeg, float yawDeg)
        {
            _pitch -= pitchUpDeg;
            _yaw += yawDeg;
        }

        public void SetAimSway(float swayPitchDeg, float swayYawDeg)
        {
            _aimSwayPitch = swayPitchDeg;
            _aimSwayYaw = swayYawDeg;
        }

        public float Pitch => _pitch;

        public Ray GetAimRay()
        {
            return new Ray(transform.position, transform.forward);
        }

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
            _smoothedCollisionDist = _currentDistance;
            _collisionDistInit = true;
            _landingDipY = 0f;
            _landingDipVel = 0f;
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
                _sprintMotion01 = 0f;
                _footStrideSpeed = 0f;
                _underwaterActive = false;
            }
            else
            {
                _nitroBoostActive = false;
                _driveCornerG = 0f;
                _surfaceVibAmp = 0f;
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
                _smoothedCollisionDist = _currentDistance;
                _collisionDistInit = true;
                _landingDipY = 0f;
                _landingDipVel = 0f;
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
                    float scale = _aimCameraActive ? _aimSensScale : 1f;
                    _yaw += look.x * scale;
                    _pitch -= look.y * scale;
                    _timeSinceManualLook = 0f;
                }
                else
                {
                    _timeSinceManualLook += Time.deltaTime;
                }
            }

            if (_aimCameraActive)
            {
                float yawDelta = Mathf.DeltaAngle(_aimYawCenter, _yaw);
                yawDelta = Mathf.Clamp(yawDelta, -_aimYawMaxHalfArc, _aimYawMaxHalfArc);
                _yaw = _aimYawCenter + yawDelta;
                _pitch = Mathf.Clamp(_pitch, _aimMinPitch, _aimMaxPitch);

                float dt = Time.deltaTime;
                Vector3 aimRight = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
                Vector3 desiredAimPivot = target.position
                    + Vector3.up * _aimShoulderOffset.y
                    + aimRight * _aimShoulderOffset.x;
                _smoothedPivot = Vector3.Lerp(_smoothedPivot, desiredAimPivot, 1f - Mathf.Exp(-18f * dt));
                _currentDistance = Mathf.Lerp(_currentDistance, _aimDistance, 1f - Mathf.Exp(-14f * dt));

                if (_camera == null) _camera = GetComponent<Camera>();
                if (_camera != null)
                {
                    float aspect = _camera.aspect > 0.1f ? _camera.aspect : (16f / 9f);
                    float tabletBoost = (_aimFov >= 28f && aspect < 1.70f)
                        ? Mathf.Clamp((1.70f - aspect) * 18f, 0f, 8f)
                        : 0f;
                    _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _aimFov + tabletBoost, 1f - Mathf.Exp(-15f * dt));
                }

                Quaternion aimRot = Quaternion.Euler(_pitch + _aimSwayPitch, _yaw + _aimSwayYaw, 0f);
                Vector3 aimShake = Vector3.zero;
                if (_shakeTimer > 0f)
                {
                    _shakeTimer -= Time.unscaledDeltaTime;
                    if (_shakeTimer <= 0f)
                    {
                        _shakeTimer = 0f;
                        _shakeIntensity = 0f;
                    }
                    else
                    {
                        float amp = _shakeIntensity * Mathf.Clamp01(_shakeTimer / Mathf.Max(0.01f, _shakeDuration));
                        aimShake = new Vector3(
                            (Mathf.PerlinNoise(Time.unscaledTime * 54f, 1.3f) - 0.5f) * 2f * amp,
                            (Mathf.PerlinNoise(2.7f, Time.unscaledTime * 54f) - 0.5f) * 2f * amp,
                            0f);
                    }
                }

                Vector3 aimCamPos;
                if (_aimDistance <= 0.15f)
                {
                    // Full-screen Sniper Scope view: sit cleanly ahead of player head so player mesh never blocks optics!
                    aimCamPos = _smoothedPivot + (aimRot * Vector3.forward) * 0.52f + aimRot * (aimShake * 0.2f);
                }
                else
                {
                    Vector3 aimBack = aimRot * Vector3.back;
                    float safeAimDist = ComputeCollisionAdjustedDistance(_smoothedPivot, aimBack, _currentDistance);
                    aimCamPos = _smoothedPivot + aimBack * safeAimDist + aimRot * aimShake;
                }

                transform.position = aimCamPos;
                transform.rotation = aimRot;
                return;
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

            float dtNorm = Time.deltaTime;
            // Critically-damped vertical spring for tactile jump/vault landing dip
            if (Mathf.Abs(_landingDipY) > 0.0005f || Mathf.Abs(_landingDipVel) > 0.005f)
            {
                const float springK = 145f;
                const float springDamp = 19.5f;
                float springAccel = -springK * _landingDipY - springDamp * _landingDipVel;
                _landingDipVel += springAccel * dtNorm;
                _landingDipY = Mathf.Clamp(_landingDipY + _landingDipVel * dtNorm, -0.26f, 0.08f);
            }
            else
            {
                _landingDipY = 0f;
                _landingDipVel = 0f;
            }

            // Smooth sprint & nitro motion channels + decaying FOV impulse + organic stride / driving G-force feel
            _smoothedSprint01 = Mathf.Lerp(_smoothedSprint01, _isDrivingMode || _sportCameraActive ? 0f : _sprintMotion01, 1f - Mathf.Exp(-6.5f * dtNorm));
            _nitroBoostBlend = Mathf.Lerp(_nitroBoostBlend, _isDrivingMode && _nitroBoostActive ? 1f : 0f, 1f - Mathf.Exp(-8.5f * dtNorm));
            _fovImpulse = Mathf.Lerp(_fovImpulse, 0f, 1f - Mathf.Exp(-7.5f * dtNorm));
            if (Mathf.Abs(_fovImpulse) < 0.02f) _fovImpulse = 0f;

            // Grounded micro stride bob (never nauseating; automatically disabled indoors, seated, sport, aiming)
            Vector3 organicMicroOffset = Vector3.zero;
            if (!_isDrivingMode && !_isSeatedMode && !_sportCameraActive && !_underwaterActive && _footStrideSpeed > 0.35f)
            {
                float paceNorm = Mathf.InverseLerp(0.35f, 4.4f, _footStrideSpeed);
                float freq = Mathf.Lerp(1.85f, 2.95f, paceNorm);
                _stridePhase += dtNorm * freq * Mathf.PI * 2f;
                if (_stridePhase > Mathf.PI * 200f) _stridePhase -= Mathf.PI * 200f;
                float ampY = Mathf.Lerp(0.011f, 0.026f, paceNorm);
                float ampX = Mathf.Lerp(0.006f, 0.014f, paceNorm);
                organicMicroOffset = new Vector3(
                    Mathf.Cos(_stridePhase * 0.5f) * ampX,
                    Mathf.Sin(_stridePhase) * ampY,
                    0f);
            }
            else if (_underwaterActive)
            {
                float uwT = Time.time;
                organicMicroOffset = new Vector3(
                    Mathf.Sin(uwT * 1.15f) * 0.032f,
                    Mathf.Cos(uwT * 0.85f) * 0.028f,
                    0f);
            }

            // Smooth driving corner roll, turn look-ahead & subtle road surface vibration
            float targetRoll = _isDrivingMode ? Mathf.Clamp(-_driveCornerG * 1.45f, -1.75f, 1.75f) : (_underwaterActive ? Mathf.Sin(Time.time * 0.72f) * 1.1f : 0f);
            _smoothedDriveRollDeg = Mathf.Lerp(_smoothedDriveRollDeg, targetRoll, 1f - Mathf.Exp(-6.2f * dtNorm));
            float targetLookAhead = (_isDrivingMode && _timeSinceManualLook > 0.65f) ? Mathf.Clamp(_driveCornerG * 3.1f, -3.4f, 3.4f) : 0f;
            _smoothedCornerLookAheadYaw = Mathf.Lerp(_smoothedCornerLookAheadYaw, targetLookAhead, 1f - Mathf.Exp(-4.2f * dtNorm));
            if (_surfaceVibDecay > 0f)
            {
                _surfaceVibDecay -= dtNorm;
                if (_surfaceVibDecay <= 0f) _surfaceVibAmp = 0f;
            }
            else
            {
                _surfaceVibAmp = Mathf.MoveTowards(_surfaceVibAmp, 0f, dtNorm * 0.14f);
            }
            if (!_isDrivingMode)
            {
                _driveCornerG = Mathf.MoveTowards(_driveCornerG, 0f, dtNorm * 6f);
            }

            Vector3 desiredOffset = _isDrivingMode
                ? drivingPivotOffset
                : (_isSeatedMode ? seatedPivotOffset : footPivotOffset);
            Vector3 desiredPivot = target.position + desiredOffset + Vector3.up * _landingDipY;
            float horizK = 1f - Mathf.Exp(-pivotFollowSpeed * dtNorm);
            // Slightly gentler vertical follow on foot smooths out stair steps & sidewalk curbs cleanly
            float vertFollow = _isDrivingMode ? pivotFollowSpeed : 11.5f;
            float vertK = 1f - Mathf.Exp(-vertFollow * dtNorm);
            _smoothedPivot = new Vector3(
                Mathf.Lerp(_smoothedPivot.x, desiredPivot.x, horizK),
                Mathf.Lerp(_smoothedPivot.y, desiredPivot.y, vertK),
                Mathf.Lerp(_smoothedPivot.z, desiredPivot.z, horizK));

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

            float footSprintPull = effectivelyIndoors ? 0f : _smoothedSprint01 * 0.34f;
            float desiredDistance = _sportCameraActive
                ? _sportCameraDistance
                : (_isDrivingMode
                    ? drivingDistance + speedFactor * drivingSpeedExtraDistance + _nitroBoostBlend * 0.62f
                    : (hasOverheadCeiling
                        ? Mathf.Min(GetEffectiveFootMaxDistance(), 1.90f)
                        : GetEffectiveFootMaxDistance() + footSprintPull));
            float distSpeed = effectivelyIndoors ? distanceBlendSpeed * 2.2f : distanceBlendSpeed;
            _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, 1f - Mathf.Exp(-distSpeed * dtNorm));

            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                float sprintFovKick = effectivelyIndoors ? 0f : _smoothedSprint01 * 4.2f;
                float nitroFovKick = _nitroBoostBlend * 6.5f;
                float baseTargetFov = _sportCameraActive
                    ? _sportCameraFov
                    : (_isDrivingMode
                        ? Mathf.Lerp(baseFov, drivingMaxFov, speedFactor) + nitroFovKick
                        : (effectivelyIndoors ? 66f : baseFov + sprintFovKick));
                // Preserve wide horizontal view on 4:3 iPads, 16:10 Android tablets & foldables
                float aspect = _camera.aspect > 0.1f ? _camera.aspect : (16f / 9f);
                float tabletFovBoost = aspect < 1.70f ? Mathf.Clamp((1.70f - aspect) * 26f, 0f, 12f) : 0f;
                float desiredFov = Mathf.Clamp(baseTargetFov + tabletFovBoost + _fovImpulse, 44f, 88f);
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, desiredFov, 1f - Mathf.Exp(-fovSmoothSpeed * dtNorm));
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw + _smoothedCornerLookAheadYaw, 0f);
            Vector3 backDir = rotation * Vector3.back;

            // Smooth wall-collision distance: fast pull-in (no wall clipping), silky ease-out (no lamppost/tree pop!)
            float rawCollisionDist = ComputeCollisionAdjustedDistance(_smoothedPivot, backDir, _currentDistance);
            if (!_collisionDistInit)
            {
                _smoothedCollisionDist = rawCollisionDist;
                _collisionDistInit = true;
            }
            else if (rawCollisionDist < _smoothedCollisionDist)
            {
                _smoothedCollisionDist = Mathf.Lerp(_smoothedCollisionDist, rawCollisionDist, 1f - Mathf.Exp(-28f * dtNorm));
                if (rawCollisionDist < _smoothedCollisionDist - 0.18f)
                {
                    _smoothedCollisionDist = rawCollisionDist;
                }
            }
            else
            {
                _smoothedCollisionDist = Mathf.Lerp(_smoothedCollisionDist, rawCollisionDist, 1f - Mathf.Exp(-6.8f * dtNorm));
            }
            float finalDistance = Mathf.Min(_smoothedCollisionDist, _currentDistance);

            Vector3 shakeOffset = Vector3.zero;
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.unscaledDeltaTime;
                if (_shakeTimer <= 0f)
                {
                    _shakeTimer = 0f;
                    _shakeIntensity = 0f;
                }
                else
                {
                    float amp = _shakeIntensity * Mathf.Clamp01(_shakeTimer / Mathf.Max(0.01f, _shakeDuration));
                    shakeOffset = new Vector3(
                        (Mathf.PerlinNoise(Time.unscaledTime * 42f, 1.3f) - 0.5f) * 2f * amp,
                        (Mathf.PerlinNoise(2.7f, Time.unscaledTime * 42f) - 0.5f) * 2f * amp,
                        0f);
                }
            }
            if (_nitroBoostBlend > 0.05f)
            {
                float nitroAmp = 0.032f * _nitroBoostBlend;
                shakeOffset += new Vector3(
                    (Mathf.PerlinNoise(Time.unscaledTime * 55f, 4.1f) - 0.5f) * 2f * nitroAmp,
                    (Mathf.PerlinNoise(7.3f, Time.unscaledTime * 55f) - 0.5f) * 2f * nitroAmp,
                    0f);
            }
            if (_surfaceVibAmp > 0.001f && _isDrivingMode)
            {
                shakeOffset += new Vector3(
                    (Mathf.PerlinNoise(Time.time * 38f, 9.2f) - 0.5f) * 2f * _surfaceVibAmp * 0.6f,
                    (Mathf.PerlinNoise(5.4f, Time.time * 44f) - 0.5f) * 2f * _surfaceVibAmp,
                    0f);
            }
            if (!effectivelyIndoors)
            {
                shakeOffset += organicMicroOffset;
            }

            Vector3 worldShake = rotation * shakeOffset;
            Vector3 finalPos = _smoothedPivot + backDir * finalDistance + worldShake;

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
                        _smoothedCollisionDist = Mathf.Min(_smoothedCollisionDist, safeDist);
                    }
                }
            }

            transform.position = finalPos;
            transform.LookAt(_smoothedPivot + worldShake * 0.45f);
            if (Mathf.Abs(_smoothedDriveRollDeg) > 0.01f)
            {
                transform.Rotate(0f, 0f, _smoothedDriveRollDeg, Space.Self);
            }
        }

        private float _shakeTimer;
        private float _shakeDuration;
        private float _shakeIntensity;

        public void AddShake(float intensity, float duration = 0.18f)
        {
            float activeAmp = _shakeTimer > 0f
                ? _shakeIntensity * Mathf.Clamp01(_shakeTimer / Mathf.Max(0.01f, _shakeDuration))
                : 0f;
            float clampedDur = Mathf.Max(0.05f, duration);
            _shakeIntensity = Mathf.Max(activeAmp, intensity);
            _shakeTimer = Mathf.Max(_shakeTimer, clampedDur);
            _shakeDuration = Mathf.Max(_shakeTimer, clampedDur);
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
