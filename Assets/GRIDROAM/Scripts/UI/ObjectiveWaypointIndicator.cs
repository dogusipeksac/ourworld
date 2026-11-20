using Gridroam.Core;
using Gridroam.Jobs;
using Gridroam.Missions;
using Gridroam.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Rotates a HUD compass arrow toward the active navigation target
    /// (active Side Job target, Phone custom waypoint, or active Story Mission anchor)
    /// and displays remaining distance in meters.
    /// </summary>
    public class ObjectiveWaypointIndicator : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private RectTransform arrowRect;
        [SerializeField] private Text distanceText;
        [SerializeField] private Graphic arrowGraphic;

        private static readonly Color MissionTint = new Color(0.92f, 0.74f, 0.34f, 1f);
        private static readonly Color AccentTealTint = new Color(0.31f, 0.70f, 0.66f, 1f);

        public void Configure(GameObject root, RectTransform arrow, Text distLabel)
        {
            container = root;
            arrowRect = arrow;
            distanceText = distLabel;
            if (arrowRect != null)
            {
                arrowGraphic = arrowRect.GetComponent<Graphic>();
            }
        }

        public static bool TryGetActiveNavigationTarget(out Vector3 worldTarget, out bool isSideOrCustomTarget)
        {
            worldTarget = Vector3.zero;
            isSideOrCustomTarget = false;

            if (JobManager.Instance != null && JobManager.Instance.HasActiveWaypoint)
            {
                worldTarget = JobManager.Instance.ActiveWaypointPosition;
                isSideOrCustomTarget = true;
                return true;
            }

            if (PhonePanelController.Instance != null && PhonePanelController.Instance.HasCustomWaypoint)
            {
                worldTarget = PhonePanelController.Instance.CustomWaypointPosition;
                isSideOrCustomTarget = true;
                return true;
            }

            MissionManager mm = MissionManager.Instance;
            if (mm != null && mm.CurrentTargetAnchor != null && !mm.IsInFreeRoam && !mm.IsFailed)
            {
                worldTarget = mm.CurrentTargetAnchor.transform.position;
                isSideOrCustomTarget = false;
                return true;
            }

            return false;
        }

        private void LateUpdate()
        {
            PlayerModeController player = PlayerModeController.Instance;
            Camera mainCam = Camera.main;

            if (player == null || mainCam == null || !TryGetActiveNavigationTarget(out Vector3 targetWorldPos, out bool isAccent))
            {
                if (container != null && container.activeSelf)
                {
                    container.SetActive(false);
                }
                return;
            }

            if (container != null && !container.activeSelf)
            {
                container.SetActive(true);
            }

            if (arrowGraphic == null && arrowRect != null)
            {
                arrowGraphic = arrowRect.GetComponent<Graphic>();
            }
            if (arrowGraphic != null)
            {
                arrowGraphic.color = isAccent ? AccentTealTint : MissionTint;
            }

            Vector3 fromPos = player.ActiveWorldPosition;
            Vector3 toTarget = targetWorldPos - fromPos;
            float distance = toTarget.magnitude;

            Vector3 flatTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            Vector3 flatCamForward = new Vector3(mainCam.transform.forward.x, 0f, mainCam.transform.forward.z);

            if (arrowRect != null && flatTarget.sqrMagnitude > 0.01f && flatCamForward.sqrMagnitude > 0.001f)
            {
                float signedAngle = Vector3.SignedAngle(flatCamForward.normalized, flatTarget.normalized, Vector3.up);
                arrowRect.localEulerAngles = new Vector3(0f, 0f, -signedAngle);
            }

            if (distanceText != null)
            {
                string unit = LocalizationService.Tr("ui.hud.distance_unit", "m");
                distanceText.text = $"{Mathf.RoundToInt(distance)} {unit}";
            }
        }
    }
}
