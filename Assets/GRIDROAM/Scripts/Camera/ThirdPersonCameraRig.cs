using Gridroam.Input;
using UnityEngine;

namespace Gridroam.CameraSystem
{
    /// <summary>
    /// Smooth third-person camera supporting both On-Foot exploration and Driving modes,
    /// touch/mouse orbit, gentle driving auto-align, and collision pull-in via SphereCast.
    /// </summary>
    public class ThirdPersonCameraRig : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Transform target;

        [Header("On-Foot Profile")]
        [SerializeField] private Vector3 footPivotOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField] private float footDistance = 4.6f;
        [SerializeField] private float footMinPitch = -18f;
        [SerializeField] private float footMaxPitch = 58f;

        [Header("Driving Profile")]
        [SerializeField] private Vector3 drivingPivotOffset = new Vector3(0f, 1.85f, 0f);
        [SerializeField] private float drivingDistance = 7.4f;
        [SerializeField] private float drivingDefaultPitch = 16f;
        [SerializeField] private float drivingAutoAlignDelay = 1.4f;
        [SerializeField] private float drivingAutoAlignSpeed = 3.2f;

        [Header("Smoothing & Collision")]
        [SerializeField] private float pivotFollowSpeed = 14f;
        [SerializeField] private float distanceBlendSpeed = 6f;
        [SerializeField] private float collisionRadius = 0.28f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private float _yaw;
        private float _pitch = 16f;
        private float _currentDistance;
        private Vector3 _smoothedPivot;
        private bool _isDrivingMode;
        private float _timeSinceManualLook = 99f;

        public float Yaw => _yaw;
        public Vector3 FlatForward => Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
        public Vector3 FlatRight => Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;

        private void Start()
        {
            _currentDistance = footDistance;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _smoothedPivot = target.position + footPivotOffset;
            }
        }

        public void SetTarget(Transform newTarget, bool drivingMode, bool snapBehindTarget = false)
        {
            target = newTarget;
            _isDrivingMode = drivingMode;

            if (target != null && snapBehindTarget)
            {
                _yaw = target.eulerAngles.y;
                _pitch = drivingMode ? drivingDefaultPitch : 14f;
                _smoothedPivot = target.position + (drivingMode ? drivingPivotOffset : footPivotOffset);
                _currentDistance = drivingMode ? drivingDistance : footDistance;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
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

            if (_isDrivingMode && _timeSinceManualLook > drivingAutoAlignDelay)
            {
                float targetYaw = target.eulerAngles.y;
                _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-drivingAutoAlignSpeed * Time.deltaTime));
                _pitch = Mathf.Lerp(_pitch, drivingDefaultPitch, 1f - Mathf.Exp(-2.2f * Time.deltaTime));
            }

            float minPitch = _isDrivingMode ? 4f : footMinPitch;
            float maxPitch = _isDrivingMode ? 42f : footMaxPitch;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Vector3 desiredOffset = _isDrivingMode ? drivingPivotOffset : footPivotOffset;
            Vector3 desiredPivot = target.position + desiredOffset;
            _smoothedPivot = Vector3.Lerp(_smoothedPivot, desiredPivot, 1f - Mathf.Exp(-pivotFollowSpeed * Time.deltaTime));

            float desiredDistance = _isDrivingMode ? drivingDistance : footDistance;
            _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, 1f - Mathf.Exp(-distanceBlendSpeed * Time.deltaTime));

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 backDir = rotation * Vector3.back;

            float finalDistance = _currentDistance;
            if (Physics.SphereCast(_smoothedPivot, collisionRadius, backDir, out RaycastHit hit, _currentDistance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Ignore colliders belonging to the tracked target hierarchy (player or sedan).
                if (!hit.transform.IsChildOf(target) && hit.transform != target)
                {
                    finalDistance = Mathf.Max(1.25f, hit.distance - 0.15f);
                }
            }

            transform.position = _smoothedPivot + backDir * finalDistance;
            transform.LookAt(_smoothedPivot);
        }
    }
}
