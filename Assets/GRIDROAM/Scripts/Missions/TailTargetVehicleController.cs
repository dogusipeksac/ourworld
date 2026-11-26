using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.Missions
{
    /// <summary>
    /// Controls the harbor courier car during Mission 4 (Night Tail / Gece Takibi).
    /// Drives from the Cargo Harbor exit down the Coastal Road, turns West onto the
    /// cross-street, and stops in the North Back Alley where it drops the sealed package.
    /// </summary>
    public class TailTargetVehicleController : MonoBehaviour
    {
        public static TailTargetVehicleController Instance { get; private set; }

        [Header("Route & Package")]
        [SerializeField] private List<Vector3> routePoints = new List<Vector3>();
        [SerializeField] private GameObject droppedPackageObject;
        [SerializeField] private float cruiseSpeedMs = 11.2f;
        [SerializeField] private float cornerSpeedMs = 7.8f;
        [SerializeField] private float turnSpeed = 5.5f;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private int _currentWaypointIndex;
        private bool _isDrivingRoute;
        private bool _hasArrived;

        public bool IsDrivingRoute => _isDrivingRoute;
        public bool HasArrived => _hasArrived;
        public GameObject DroppedPackageObject => droppedPackageObject;

        public void Configure(List<Vector3> route, GameObject packageObj)
        {
            routePoints = route;
            droppedPackageObject = packageObj;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void Start()
        {
            if (droppedPackageObject != null)
            {
                droppedPackageObject.SetActive(false);
            }

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnObjectiveChanged += HandleObjectiveChanged;
                MissionManager.Instance.OnMissionFailed += HandleMissionFailed;
                SyncWithCurrentMissionStep(MissionManager.Instance.ActiveMission, MissionManager.Instance.CurrentStep);
            }
        }

        private void OnDestroy()
        {
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnObjectiveChanged -= HandleObjectiveChanged;
                MissionManager.Instance.OnMissionFailed -= HandleMissionFailed;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleMissionFailed(string _)
        {
            _isDrivingRoute = false;
        }

        private void HandleObjectiveChanged(MissionData mission, MissionStepData step)
        {
            SyncWithCurrentMissionStep(mission, step);
        }

        private void SyncWithCurrentMissionStep(MissionData mission, MissionStepData step)
        {
            if (mission == null || step == null)
            {
                _isDrivingRoute = false;
                if (droppedPackageObject != null)
                {
                    droppedPackageObject.SetActive(false);
                }
                return;
            }

            if (step.stepType == MissionStepType.TailVehicle &&
                string.Equals(step.targetAnchorId, "tail_target_car", System.StringComparison.OrdinalIgnoreCase))
            {
                BeginTailRun();
                return;
            }

            if (string.Equals(step.targetAnchorId, "tail_dropped_package", System.StringComparison.OrdinalIgnoreCase))
            {
                _isDrivingRoute = false;
                _hasArrived = true;
                if (routePoints != null && routePoints.Count > 0)
                {
                    Vector3 lastPt = routePoints[routePoints.Count - 1];
                    transform.SetPositionAndRotation(lastPt, Quaternion.Euler(0f, 0f, 0f));
                }
                if (droppedPackageObject != null)
                {
                    droppedPackageObject.SetActive(true);
                    if (PlayerModeController.Instance != null)
                    {
                        PlayerModeController.Instance.RefreshInteractablesCache();
                    }
                }
                return;
            }

            if (string.Equals(mission.missionId, "m04_night_tail", System.StringComparison.OrdinalIgnoreCase) &&
                step.stepType == MissionStepType.ReachLocation)
            {
                ResetToHarborGate();
            }
            else if (droppedPackageObject != null && droppedPackageObject.activeSelf)
            {
                droppedPackageObject.SetActive(false);
            }
        }

        public void ResetToHarborGate()
        {
            _isDrivingRoute = false;
            _hasArrived = false;
            _currentWaypointIndex = 0;
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
            if (droppedPackageObject != null)
            {
                droppedPackageObject.SetActive(false);
            }
        }

        public void BeginTailRun()
        {
            if (routePoints == null || routePoints.Count < 2)
            {
                return;
            }

            transform.position = routePoints[0];
            Vector3 firstDir = routePoints[1] - routePoints[0];
            firstDir.y = 0f;
            if (firstDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(firstDir.normalized, Vector3.up);
            }

            _currentWaypointIndex = 1;
            _hasArrived = false;
            _isDrivingRoute = true;

            if (droppedPackageObject != null)
            {
                droppedPackageObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_isDrivingRoute || _hasArrived || routePoints == null || _currentWaypointIndex >= routePoints.Count)
            {
                return;
            }

            if (MissionManager.Instance != null && (MissionManager.Instance.IsFailed || MissionManager.Instance.IsDialoguePlaying))
            {
                return;
            }

            Vector3 targetPos = routePoints[_currentWaypointIndex];
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            float distToWp = toTarget.magnitude;

            if (distToWp < 2.2f)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= routePoints.Count)
                {
                    ArriveAndDropPackage();
                    return;
                }
                targetPos = routePoints[_currentWaypointIndex];
                toTarget = targetPos - transform.position;
                toTarget.y = 0f;
                distToWp = Mathf.Max(0.01f, toTarget.magnitude);
            }

            Vector3 dir = toTarget / distToWp;
            Quaternion desiredRot = Quaternion.LookRotation(dir, Vector3.up);
            float angleDiff = Quaternion.Angle(transform.rotation, desiredRot);
            Quaternion nextRot = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * turnSpeed);

            float targetSpeed = angleDiff > 18f || distToWp < 12f ? cornerSpeedMs : cruiseSpeedMs;
            Vector3 nextPos = transform.position + dir * (targetSpeed * Time.deltaTime);
            transform.SetPositionAndRotation(nextPos, nextRot);
        }

        private void ArriveAndDropPackage()
        {
            _isDrivingRoute = false;
            _hasArrived = true;

            if (droppedPackageObject != null)
            {
                droppedPackageObject.SetActive(true);
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RefreshInteractablesCache();
                }
            }

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.NotifyTailTargetArrived();
            }
        }
    }
}
