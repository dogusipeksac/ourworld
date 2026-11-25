using OurWorld.CameraSystem;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Interior trigger volume for enterable houses and shops.
    /// Keeps interior point light(s) disabled while outside for mobile performance,
    /// enables them when the player enters, and pulls ThirdPersonCameraRig max distance in.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class InteriorZone : MonoBehaviour
    {
        [SerializeField] private Light[] interiorLights;
        [SerializeField] private float indoorCameraMaxDistance = 2.2f;

        private BoxCollider _zoneBox;
        private ThirdPersonCameraRig _cameraRig;
        private float _nextCamRigLookupTime;
        private bool _playerInside;

        public bool IsPlayerInside => _playerInside;

        public void Configure(Light[] lights, float indoorMaxDistance = 2.2f)
        {
            interiorLights = lights;
            indoorCameraMaxDistance = indoorMaxDistance;
            CalibrateInteriorLights();
            SetLightsEnabled(false);
        }

        private void Awake()
        {
            _zoneBox = GetComponent<BoxCollider>();
            if (_zoneBox != null)
            {
                _zoneBox.isTrigger = true;
            }
            CalibrateInteriorLights();
            SetLightsEnabled(false);
        }

        private void CalibrateInteriorLights()
        {
            if (interiorLights == null) return;
            for (int i = 0; i < interiorLights.Length; i++)
            {
                Light l = interiorLights[i];
                if (l == null || l.type != LightType.Point) continue;
                // Calibrate legacy gamma-era indoor point lights (~2.65) for crisp URP Linear room illumination
                if (l.intensity < 12f)
                {
                    l.intensity = Mathf.Clamp(l.intensity * 6.8f, 16f, 24f);
                }
                if (l.range < 11f)
                {
                    l.range = 11f;
                }
                l.shadows = LightShadows.None;
            }
        }

        private void Start()
        {
            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            SetLightsEnabled(false);
        }

        private void OnDisable()
        {
            if (_playerInside)
            {
                _playerInside = false;
                SetLightsEnabled(false);
                if (_cameraRig != null)
                {
                    _cameraRig.SetIndoorZone(this, false, indoorCameraMaxDistance);
                }
            }
        }

        private void Update()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || _zoneBox == null)
            {
                return;
            }

            bool insideNow = false;
            if (!mode.IsDriving && mode.FootController != null)
            {
                Vector3 sampleWorld = mode.FootController.transform.position + Vector3.up * 0.65f;
                Vector3 local = transform.InverseTransformPoint(sampleWorld) - _zoneBox.center;
                Vector3 half = _zoneBox.size * 0.5f;
                insideNow = Mathf.Abs(local.x) <= half.x &&
                            Mathf.Abs(local.y) <= half.y &&
                            Mathf.Abs(local.z) <= half.z;
            }

            if (insideNow != _playerInside)
            {
                _playerInside = insideNow;
                SetLightsEnabled(_playerInside);

                if (_cameraRig == null && Time.time >= _nextCamRigLookupTime)
                {
                    _nextCamRigLookupTime = Time.time + 1f;
                    _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
                }
                if (_cameraRig != null)
                {
                    _cameraRig.SetIndoorZone(this, _playerInside, indoorCameraMaxDistance);
                }
            }
        }

        private void SetLightsEnabled(bool enabledState)
        {
            if (interiorLights == null) return;
            for (int i = 0; i < interiorLights.Length; i++)
            {
                if (interiorLights[i] != null)
                {
                    interiorLights[i].enabled = enabledState;
                }
            }
        }
    }
}
