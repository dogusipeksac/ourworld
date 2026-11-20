using Gridroam.Core;
using Gridroam.Missions;
using Gridroam.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Rotates a HUD compass arrow toward the active mission target anchor
    /// and displays remaining distance in meters.
    /// </summary>
    public class ObjectiveWaypointIndicator : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private RectTransform arrowRect;
        [SerializeField] private Text distanceText;

        public void Configure(GameObject root, RectTransform arrow, Text distLabel)
        {
            container = root;
            arrowRect = arrow;
            distanceText = distLabel;
        }

        private void LateUpdate()
        {
            MissionManager mm = MissionManager.Instance;
            PlayerModeController player = PlayerModeController.Instance;
            Camera mainCam = Camera.main;

            if (mm == null || player == null || mainCam == null || mm.CurrentTargetAnchor == null || mm.IsInFreeRoam || mm.IsFailed)
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

            Vector3 fromPos = player.ActiveWorldPosition;
            Vector3 toTarget = mm.CurrentTargetAnchor.transform.position - fromPos;
            float distance = toTarget.magnitude;

            Vector3 flatTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            Vector3 flatCamForward = new Vector3(mainCam.transform.forward.x, 0f, mainCam.transform.forward.z);

            if (arrowRect != null && flatTarget.sqrMagnitude > 0.01f && flatCamForward.sqrMagnitude > 0.001f)
            {
                float signedAngle = Vector3.SignedAngle(flatCamForward.normalized, flatTarget.normalized, Vector3.up);
                // UI Z-rotation is counter-clockwise positive, world yaw is clockwise positive.
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
