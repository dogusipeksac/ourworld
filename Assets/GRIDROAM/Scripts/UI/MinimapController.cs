using System.Collections.Generic;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// GTA 6-grade circular top-left live pursuit radar using static TownMap Texture2D:
    /// - Speed-based dynamic radar zoom (smoothly pulls out when driving fast).
    /// - Live flashing Red/Blue Police cruiser & officer blips + flashing radar rim during Wanted chases.
    /// - Pulsing Blue Street Crime blip (Purse Snatcher / World Event) + active GPS waypoint clamped to rim.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        public const float MapWorldMinX = -235f;
        public const float MapWorldMaxX = 225f;
        public const float MapWorldMinZ = -228f;
        public const float MapWorldMaxZ = 232f;
        public const float MapWorldSpan = 460f;

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
        private static readonly Color PoliceRedColor = new Color(1.0f, 0.18f, 0.22f, 0.98f);
        private static readonly Color PoliceBlueColor = new Color(0.18f, 0.56f, 1.0f, 0.98f);
        private static readonly Color CrimeBlipColor = new Color(0.22f, 0.86f, 1.0f, 0.98f);
        private static readonly Color HostileBlipColor = new Color(1.0f, 0.20f, 0.16f, 0.98f);
        private static readonly Color BossBlipColor = new Color(1.0f, 0.82f, 0.14f, 1.0f);

        private Camera _cachedMainCam;
        private float _currentDynamicSpan = 190f;
        private Image[] _policeBlips;
        private Image[] _hostileBlips;
        private Image _bossBlip;
        private Image _crimeBlip;
        private Image _wantedRimGlow;
        private readonly List<Vector3> _hostilePosScratch = new List<Vector3>(8);
        private bool _radarOverlaysBuilt;

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

        private void EnsureGtaRadarOverlays()
        {
            if (_radarOverlaysBuilt || waypointDotRect == null || waypointDotRect.parent == null) return;
            _radarOverlaysBuilt = true;
            Transform overlayParent = waypointDotRect.parent;

            // Subtle Red/Blue flashing radar rim tint during Wanted pursuit!
            GameObject rimGo = new GameObject("Radar_WantedTint", typeof(RectTransform), typeof(Image));
            rimGo.transform.SetParent(overlayParent, false);
            rimGo.transform.SetAsFirstSibling();
            RectTransform rrt = (RectTransform)rimGo.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;
            _wantedRimGlow = rimGo.GetComponent<Image>();
            _wantedRimGlow.raycastTarget = false;
            _wantedRimGlow.color = new Color(1f, 0.15f, 0.2f, 0f);
            rimGo.SetActive(false);

            // Pool of 6 Police radar blips
            _policeBlips = new Image[6];
            for (int i = 0; i < _policeBlips.Length; i++)
            {
                GameObject go = new GameObject($"Radar_PoliceBlip_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(overlayParent, false);
                RectTransform brt = (RectTransform)go.transform;
                brt.anchorMin = new Vector2(0.5f, 0.5f);
                brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(11f, 11f);
                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = PoliceRedColor;
                go.SetActive(false);
                _policeBlips[i] = img;
            }

            // World Crime (Purse Snatcher) blue radar blip
            GameObject cGo = new GameObject("Radar_CrimeBlip", typeof(RectTransform), typeof(Image));
            cGo.transform.SetParent(overlayParent, false);
            RectTransform crt = (RectTransform)cGo.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(12f, 12f);
            _crimeBlip = cGo.GetComponent<Image>();
            _crimeBlip.raycastTarget = false;
            _crimeBlip.color = CrimeBlipColor;
            cGo.SetActive(false);

            // Pool of 4 crimson Story Hostile radar blips + 1 larger gold-crimson Boss radar blip
            _hostileBlips = new Image[4];
            for (int i = 0; i < _hostileBlips.Length; i++)
            {
                GameObject hGo = new GameObject($"Radar_HostileBlip_{i}", typeof(RectTransform), typeof(Image));
                hGo.transform.SetParent(overlayParent, false);
                RectTransform hrt = (RectTransform)hGo.transform;
                hrt.anchorMin = new Vector2(0.5f, 0.5f);
                hrt.anchorMax = new Vector2(0.5f, 0.5f);
                hrt.sizeDelta = new Vector2(10f, 10f);
                Image img = hGo.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = HostileBlipColor;
                hGo.SetActive(false);
                _hostileBlips[i] = img;
            }

            GameObject bGo = new GameObject("Radar_BossBlip", typeof(RectTransform), typeof(Image));
            bGo.transform.SetParent(overlayParent, false);
            RectTransform brtBoss = (RectTransform)bGo.transform;
            brtBoss.anchorMin = new Vector2(0.5f, 0.5f);
            brtBoss.anchorMax = new Vector2(0.5f, 0.5f);
            brtBoss.sizeDelta = new Vector2(15f, 15f);
            _bossBlip = bGo.GetComponent<Image>();
            _bossBlip.raycastTarget = false;
            _bossBlip.color = BossBlipColor;
            bGo.SetActive(false);
        }

        private Vector2 ProjectRadarOffset(Vector3 targetPos, Vector3 playerPos, float camYaw, float activeSpan)
        {
            float dx = targetPos.x - playerPos.x;
            float dz = targetPos.z - playerPos.z;
            float rad = camYaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float rx = dx * cos - dz * sin;
            float ry = dx * sin + dz * cos;
            float pixelsPerMeter = rotatorPixelSize / Mathf.Max(10f, activeSpan);
            return Vector2.ClampMagnitude(new Vector2(rx * pixelsPerMeter, ry * pixelsPerMeter), rimClampRadiusPx);
        }

        private void LateUpdate()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (_cachedMainCam == null) _cachedMainCam = Camera.main;
            Camera mainCam = _cachedMainCam;
            if (mode == null || mapRawImage == null || mapRotator == null)
            {
                return;
            }

            EnsureGtaRadarOverlays();

            Vector3 playerPos = mode.ActiveWorldPosition;
            float camYaw = mainCam != null ? mainCam.transform.eulerAngles.y : 0f;

            // GTA Speed-Based Dynamic Radar Zoom (pulls out cleanly as speed rises!)
            float speedKmh = (mode.IsDriving && mode.CurrentVehicle != null) ? mode.CurrentVehicle.SpeedKmh : 0f;
            float targetSpan = visibleWorldSpanOnRotator + Mathf.InverseLerp(20f, 110f, speedKmh) * 95f;
            _currentDynamicSpan = Mathf.Lerp(_currentDynamicSpan, targetSpan, 1f - Mathf.Exp(-4.5f * Time.unscaledDeltaTime));

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
            float uvSpan = _currentDynamicSpan / MapWorldSpan;
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

                    waypointDotRect.anchoredPosition = ProjectRadarOffset(targetPos, playerPos, camYaw, _currentDynamicSpan);
                }
                else if (waypointDotRect.gameObject.activeSelf)
                {
                    waypointDotRect.gameObject.SetActive(false);
                }
            }

            // Update GTA 6 Police Pursuit Blips & Flashing Rim Tint
            PoliceWantedSystem police = PoliceWantedSystem.Instance;
            bool redPhase = Mathf.FloorToInt(Time.unscaledTime * 7.5f) % 2 == 0;
            if (_wantedRimGlow != null)
            {
                bool wanted = police != null && police.IsWanted;
                if (_wantedRimGlow.gameObject.activeSelf != wanted) _wantedRimGlow.gameObject.SetActive(wanted);
                if (wanted)
                {
                    if (police.IsInPoliceSight)
                    {
                        _wantedRimGlow.color = redPhase
                            ? new Color(1.0f, 0.14f, 0.18f, 0.22f)
                            : new Color(0.14f, 0.46f, 1.0f, 0.22f);
                    }
                    else
                    {
                        float pAlpha = 0.08f + 0.09f * Mathf.Sin(Time.unscaledTime * 4.5f);
                        _wantedRimGlow.color = new Color(0.28f, 0.88f, 0.96f, pAlpha);
                    }
                }
            }

            if (_policeBlips != null)
            {
                IReadOnlyList<Vector3> units = police != null ? police.ActivePolicePositions : null;
                int unitCount = units != null ? units.Count : 0;
                for (int i = 0; i < _policeBlips.Length; i++)
                {
                    Image blip = _policeBlips[i];
                    if (blip == null) continue;
                    bool active = i < unitCount;
                    if (blip.gameObject.activeSelf != active) blip.gameObject.SetActive(active);
                    if (active)
                    {
                        blip.rectTransform.anchoredPosition = ProjectRadarOffset(units[i], playerPos, camYaw, _currentDynamicSpan);
                        blip.color = ((i % 2 == 0) == redPhase) ? PoliceRedColor : PoliceBlueColor;
                    }
                }
            }

            // Update Dynamic Street Crime (Purse Snatcher) Blue Radar Blip
            if (_crimeBlip != null)
            {
                GtaSandboxActivities gta = GtaSandboxActivities.Instance;
                bool hasCrime = gta != null && gta.HasActiveCrimeEvent;
                if (_crimeBlip.gameObject.activeSelf != hasCrime) _crimeBlip.gameObject.SetActive(hasCrime);
                if (hasCrime)
                {
                    _crimeBlip.rectTransform.anchoredPosition = ProjectRadarOffset(gta.ActiveCrimePosition, playerPos, camYaw, _currentDynamicSpan);
                    float s = 1.0f + Mathf.Sin(Time.unscaledTime * 6f) * 0.22f;
                    _crimeBlip.rectTransform.localScale = new Vector3(s, s, 1f);
                }
            }

            // Update Active Story Gang Hostile & Boss Radar Blips
            if (_hostileBlips != null)
            {
                ChapterSevenEightStoryExpansion story = ChapterSevenEightStoryExpansion.Instance;
                Vector3 bossPos = Vector3.zero;
                bool hasBoss = false;
                if (story != null)
                {
                    story.CopyActiveHostileRadarBlips(_hostilePosScratch, out bossPos, out hasBoss);
                }
                else
                {
                    _hostilePosScratch.Clear();
                }

                for (int i = 0; i < _hostileBlips.Length; i++)
                {
                    Image hBlip = _hostileBlips[i];
                    if (hBlip == null) continue;
                    bool active = i < _hostilePosScratch.Count;
                    if (hBlip.gameObject.activeSelf != active) hBlip.gameObject.SetActive(active);
                    if (active)
                    {
                        hBlip.rectTransform.anchoredPosition = ProjectRadarOffset(_hostilePosScratch[i], playerPos, camYaw, _currentDynamicSpan);
                    }
                }

                if (_bossBlip != null)
                {
                    if (_bossBlip.gameObject.activeSelf != hasBoss) _bossBlip.gameObject.SetActive(hasBoss);
                    if (hasBoss)
                    {
                        _bossBlip.rectTransform.anchoredPosition = ProjectRadarOffset(bossPos, playerPos, camYaw, _currentDynamicSpan);
                        float bs = 1.05f + Mathf.Sin(Time.unscaledTime * 7.5f) * 0.24f;
                        _bossBlip.rectTransform.localScale = new Vector3(bs, bs, 1f);
                        _bossBlip.color = redPhase ? BossBlipColor : HostileBlipColor;
                    }
                }
            }
        }
    }
}
