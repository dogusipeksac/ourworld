using System;
using System.Collections.Generic;
using Gridroam.Input;
using UnityEngine;

namespace Gridroam.Vehicle
{
    /// <summary>
    /// Axle definition structured after Unity 6's official WheelCollider Tutorial.
    /// Assign separate wheel visual transforms (e.g., from PolyDrive FREE) to sync pose automatically.
    /// </summary>
    [Serializable]
    public class AxleInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public Transform leftWheelVisual;
        public Transform rightWheelVisual;
        public bool motor;
        public bool steering;
    }

    /// <summary>
    /// Arcade-tuned WheelCollider sedan controller built for mobile touch (simultaneous steer + gas/brake)
    /// and Unity Editor keyboard testing.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SedanVehicleController : MonoBehaviour
    {
        [Header("Axles (Unity 6 WheelCollider Tutorial Pattern)")]
        [SerializeField] private List<AxleInfo> axleInfos = new List<AxleInfo>();

        [Header("Handling")]
        [SerializeField] private float maxMotorTorque = 1550f;
        [SerializeField] private float maxReverseTorque = 900f;
        [SerializeField] private float maxBrakeTorque = 2800f;
        [SerializeField] private float parkedBrakeTorque = 3600f;
        [SerializeField] private float maxSteeringAngleLowSpeed = 34f;
        [SerializeField] private float maxSteeringAngleHighSpeed = 16f;
        [SerializeField] private float maxForwardSpeedKmh = 88f;
        [SerializeField] private float maxReverseSpeedKmh = 26f;
        [SerializeField] private float steerResponseSpeed = 8f;

        [Header("Stability")]
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.42f, 0.12f);
        [SerializeField] private float antiRollForce = 4200f;
        [SerializeField] private float downforceFactor = 18f;

        [Header("Entry / Exit Points")]
        [SerializeField] private Transform leftExitPoint;
        [SerializeField] private Transform rightExitPoint;

        [Header("Visual Replacement Slot")]
        [Tooltip("Drop PolyDrive FREE sedan body visual under this transform and remove placeholder meshes.")]
        [SerializeField] private Transform bodyVisualRoot;

        private Rigidbody _rb;
        private bool _isPlayerDriving;
        private bool _inputLocked;
        private float _currentSteerAngle;

        public bool IsPlayerDriving => _isPlayerDriving;
        public float SpeedKmh => _rb != null ? _rb.linearVelocity.magnitude * 3.6f : 0f;
        public float ForwardSpeedMs => _rb != null ? Vector3.Dot(_rb.linearVelocity, transform.forward) : 0f;
        public Transform BodyVisualRoot => bodyVisualRoot;
        public List<AxleInfo> AxleInfos => axleInfos;

        public void ConfigureSetup(
            List<AxleInfo> axles,
            Transform bodyRoot,
            Transform leftExit,
            Transform rightExit)
        {
            axleInfos = axles;
            bodyVisualRoot = bodyRoot;
            leftExitPoint = leftExit;
            rightExitPoint = rightExit;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = centerOfMassOffset;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void SetPlayerDriving(bool driving)
        {
            _isPlayerDriving = driving;
            if (!driving && InputHub.Instance != null)
            {
                InputHub.Instance.ClearDrivingTouchInputs();
            }
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
        }

        public Vector3 GetSafeExitPosition(float playerRadius = 0.42f, float playerHeight = 1.8f)
        {
            Transform[] candidates = { leftExitPoint, rightExitPoint };
            foreach (Transform candidate in candidates)
            {
                if (candidate == null) continue;
                Vector3 pos = candidate.position;
                Vector3 bottom = pos + Vector3.up * (playerRadius + 0.1f);
                Vector3 top = pos + Vector3.up * Mathf.Max(playerHeight - playerRadius, playerRadius + 0.2f);
                if (!Physics.CheckCapsule(bottom, top, playerRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    return pos;
                }
            }

            return transform.position + (-transform.right * 2.2f) + Vector3.up * 0.2f;
        }

        public void TeleportAndReset(Vector3 position, Quaternion rotation)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            _currentSteerAngle = 0f;
        }

        private void FixedUpdate()
        {
            if (_rb == null || axleInfos == null || axleInfos.Count == 0)
            {
                return;
            }

            float speedKmh = SpeedKmh;
            float forwardMs = ForwardSpeedMs;

            float steerInput = 0f;
            float throttleInput = 0f;
            float brakeInput = 0f;
            bool handbrake = false;

            if (_isPlayerDriving && !_inputLocked && InputHub.Instance != null)
            {
                steerInput = InputHub.Instance.DriveSteer;
                throttleInput = InputHub.Instance.DriveThrottle;
                brakeInput = InputHub.Instance.DriveBrake;
                handbrake = InputHub.Instance.HandbrakeHeld;
            }

            float speedLerp = Mathf.InverseLerp(8f, maxForwardSpeedKmh, speedKmh);
            float maxSteerAtSpeed = Mathf.Lerp(maxSteeringAngleLowSpeed, maxSteeringAngleHighSpeed, speedLerp);
            float targetSteerAngle = steerInput * maxSteerAtSpeed;
            _currentSteerAngle = Mathf.Lerp(_currentSteerAngle, targetSteerAngle, Time.fixedDeltaTime * steerResponseSpeed);

            float motorTorque = 0f;
            float brakeTorque = 0f;

            if (!_isPlayerDriving || _inputLocked)
            {
                brakeTorque = parkedBrakeTorque;
            }
            else if (handbrake)
            {
                brakeTorque = parkedBrakeTorque;
            }
            else if (throttleInput > 0.01f)
            {
                if (forwardMs < -1.0f)
                {
                    // Braking from reverse before accelerating forward.
                    brakeTorque = maxBrakeTorque * throttleInput;
                }
                else if (speedKmh < maxForwardSpeedKmh)
                {
                    motorTorque = maxMotorTorque * throttleInput;
                }
            }
            else if (brakeInput > 0.01f)
            {
                if (forwardMs > 0.75f)
                {
                    brakeTorque = maxBrakeTorque * brakeInput;
                }
                else if (speedKmh < maxReverseSpeedKmh)
                {
                    motorTorque = -maxReverseTorque * brakeInput;
                }
            }
            else
            {
                // Gentle engine drag when coasting so car doesn't roll indefinitely on slopes.
                brakeTorque = 180f;
            }

            foreach (AxleInfo axle in axleInfos)
            {
                if (axle.leftWheel == null || axle.rightWheel == null) continue;

                if (axle.steering)
                {
                    axle.leftWheel.steerAngle = _currentSteerAngle;
                    axle.rightWheel.steerAngle = _currentSteerAngle;
                }

                if (axle.motor)
                {
                    axle.leftWheel.motorTorque = motorTorque;
                    axle.rightWheel.motorTorque = motorTorque;
                }
                else
                {
                    axle.leftWheel.motorTorque = 0f;
                    axle.rightWheel.motorTorque = 0f;
                }

                axle.leftWheel.brakeTorque = brakeTorque;
                axle.rightWheel.brakeTorque = brakeTorque;

                ApplyAntiRoll(axle.leftWheel, axle.rightWheel);
                ApplyLocalPositionToVisuals(axle.leftWheel, axle.leftWheelVisual);
                ApplyLocalPositionToVisuals(axle.rightWheel, axle.rightWheelVisual);
            }

            // Subtle downforce improves tire grip at higher speed on coastal curves.
            _rb.AddForce(-transform.up * (downforceFactor * _rb.linearVelocity.magnitude));
        }

        private void ApplyAntiRoll(WheelCollider leftWheel, WheelCollider rightWheel)
        {
            float travelL = 1f;
            float travelR = 1f;

            bool groundedL = leftWheel.GetGroundHit(out WheelHit hitL);
            if (groundedL)
            {
                travelL = (-leftWheel.transform.InverseTransformPoint(hitL.point).y - leftWheel.radius) /
                          Mathf.Max(0.01f, leftWheel.suspensionDistance);
            }

            bool groundedR = rightWheel.GetGroundHit(out WheelHit hitR);
            if (groundedR)
            {
                travelR = (-rightWheel.transform.InverseTransformPoint(hitR.point).y - rightWheel.radius) /
                          Mathf.Max(0.01f, rightWheel.suspensionDistance);
            }

            float antiRoll = (travelL - travelR) * antiRollForce;
            if (groundedL)
            {
                _rb.AddForceAtPosition(leftWheel.transform.up * -antiRoll, leftWheel.transform.position);
            }
            if (groundedR)
            {
                _rb.AddForceAtPosition(rightWheel.transform.up * antiRoll, rightWheel.transform.position);
            }
        }

        /// <summary>
        /// Syncs each WheelCollider's physics pose to its visual wheel transform (Unity 6 official pattern).
        /// </summary>
        private static void ApplyLocalPositionToVisuals(WheelCollider collider, Transform visualWheel)
        {
            if (visualWheel == null)
            {
                return;
            }

            collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            visualWheel.position = position;
            visualWheel.rotation = rotation;
        }
    }
}
