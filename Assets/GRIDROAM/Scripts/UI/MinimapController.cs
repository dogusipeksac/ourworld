using Gridroam.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// Lightweight circular top-left minimap using a single static TownMap Texture2D
    /// (UI RawImage + uvRect + Z-rotation by camera yaw, zero runtime cameras).
    /// Displays the player arrow at centre and projects/clamps the active waypoint onto the rim.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        public const float MapWorldMinX = -200f;
        public const float MapWorldMaxX = 180f;
        public const float MapWorldMinZ = -65f;
        public const float MapWorldMaxZ = 315f;
        public const float MapWorldSpan = 380f;

        [SerializeField] private RectTransform mapRotator;
        [SerializeField] private RawImage mapRawImage;
        [SerializeField] private RectTransform playerArrowRect;
        [SerializeField] private RectTransform waypointDotRect;
        [SerializeField] private Graphic waypointDotGraphic;
        [SerializeField] private float rotatorPixelSize = 244f;
        [SerializeField] private float visibleWorldSpanOnRotator = 190f;
        [SerializeField] private float rimClampRadiusPx = 70f;

        private static readonly Color MissionDotColor = new Color(0.94f, 0.75f, 0.32f, 0.96f);
        private static readonly Color AccentDotColor = new Color(0.31f, 0.70f, 0.66f, 0.96f);

        public void Configure(
            RectTransform rotator,
            RawImage rawImg,
            RectTransform playerArrow,
            RectTransform waypointDot,
            Graphic waypointGraphic = null)
        {
            mapRotator = rotator;
            mapRawImage = rawImg;
            playerArrowRect = playerArrow;
            waypointDotRect = waypointDot;
            waypointDotGraphic = waypointGraphic;
        }

        public static Vector2 WorldToNormalizedMapUv(Vector3 worldPos)
        {
            float u = (worldPos.x - MapWorldMinX) / MapWorldSpan;
            float v = (worldPos.z - MapWorldMinZ) / MapWorldSpan;
            return new Vector2(u, v);
        }

        private void LateUpdate()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            Camera mainCam = Camera.main;
            if (mode == null || mapRawImage == null || mapRotator == null)
            {
                return;
            }

            Vector3 playerPos = mode.ActiveWorldPosition;
            float camYaw = mainCam != null ? mainCam.transform.eulerAngles.y : 0f;

            float actorYaw = camYaw;
            if (mode.IsDriving && mode.CurrentVehicle != null)
            {
                actorYaw = mode.CurrentVehicle.transform.eulerAngles.y;
            }
            else if (mode.FootController != null)
            {
                actorYaw = mode.FootController.transform.eulerAngles.y;
            }

            Vector2 centerUv = WorldToNormalizedMapUv(playerPos);
            float uvSpan = visibleWorldSpanOnRotator / MapWorldSpan;
            float halfUv = uvSpan * 0.5f;
            mapRawImage.uvRect = new Rect(centerUv.x - halfUv, centerUv.y - halfUv, uvSpan, uvSpan);

            mapRotator.localEulerAngles = new Vector3(0f, 0f, camYaw);

            if (playerArrowRect != null)
            {
                playerArrowRect.localEulerAngles = new Vector3(0f, 0f, camYaw - actorYaw);
            }

            if (waypointDotRect != null)
            {
                if (ObjectiveWaypointIndicator.TryGetActiveNavigationTarget(out Vector3 targetPos, out bool isAccent))
                {
                    if (!waypointDotRect.gameObject.activeSelf)
                    {
                        waypointDotRect.gameObject.SetActive(true);
                    }

                    if (waypointDotGraphic != null)
                    {
                        waypointDotGraphic.color = isAccent ? AccentDotColor : MissionDotColor;
                    }

                    float dx = targetPos.x - playerPos.x;
                    float dz = targetPos.z - playerPos.z;
                    float rad = camYaw * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);

                    float rx = dx * cos - dz * sin;
                    float ry = dx * sin + dz * cos;

                    float pixelsPerMeter = rotatorPixelSize / Mathf.Max(10f, visibleWorldSpanOnRotator);
                    Vector2 offsetPx = new Vector2(rx * pixelsPerMeter, ry * pixelsPerMeter);
                    waypointDotRect.anchoredPosition = Vector2.ClampMagnitude(offsetPx, rimClampRadiusPx);
                }
                else if (waypointDotRect.gameObject.activeSelf)
                {
                    waypointDotRect.gameObject.SetActive(false);
                }
            }
        }
    }
}
