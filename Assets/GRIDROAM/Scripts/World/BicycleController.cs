using System;
using OurWorld.Input;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class BicycleController : MonoBehaviour
    {
        private const float WheelRadius = 0.34f;
        private const float TopSpeed = 7.8f;
        private const float CrankRadius = 0.15f;
        // Wheel revolutions per crank revolution (chainring/sprocket). ~84 rpm cadence at top speed.
        private const float PedalGearRatio = 2.6f;

        private CharacterController _body;
        private Transform _visual;
        private Transform _fork;
        private Transform _frontWheel;
        private Transform _rearWheel;
        private Transform _crank;
        private PlayerFootController _rider;
        private float _speed;
        private float _fallSpeed;
        private float _wheelAngle;
        private float _crankAngle;
        private float _crankSpinRate;
        private float _lean;
        private float _pitch;
        private float _smoothedSteer;
        private float _accelLean;
        private float _mountProgress = 1f;
        private Vector3 _mountPosition;
        private Quaternion _mountRotation;
        private Action _dismountFinished;
        private AudioSource _engine;

        public float SpeedKmh => Mathf.Abs(_speed) * 3.6f;
        public float MaxSpeedKmh => (IsMotorcycle ? 20f : TopSpeed) * 3.6f;
        public bool IsMotorcycle { get; private set; }
        public bool IsMounting => _mountProgress < 1f && _dismountFinished == null;
        public bool IsTransitioning => IsMounting || _dismountFinished != null;

        public void Configure(Transform visual, Transform fork, Transform frontWheel, Transform rearWheel, Transform crank)
        {
            _visual = visual;
            _fork = fork;
            _frontWheel = frontWheel;
            _rearWheel = rearWheel;
            _crank = crank;
            _body = GetComponent<CharacterController>();
            _body.radius = 0.31f;
            _body.height = 1.62f;
            _body.center = Vector3.up * 0.81f;
            _body.stepOffset = 0.22f;
            _body.slopeLimit = 35f;
            _body.minMoveDistance = 0f;
            enabled = false;
        }

        public void ConfigureMotorcycle(Transform visual, Transform fork, Transform frontWheel, Transform rearWheel,
            AudioSource engine)
        {
            Configure(visual, fork, frontWheel, rearWheel, null);
            IsMotorcycle = true;
            _engine = engine;
            _body.radius = 0.42f;
            _body.height = 1.75f;
            _body.center = Vector3.up * 0.875f;
            _body.stepOffset = 0.12f;
        }

        public void Mount(PlayerFootController rider)
        {
            _rider = rider;
            _speed = 0f;
            _fallSpeed = 0f;
            _crankSpinRate = 0f;
            _smoothedSteer = 0f;
            _pitch = 0f;
            _accelLean = 0f;
            _mountPosition = rider.transform.position;
            _mountRotation = rider.transform.rotation;
            _mountProgress = 0f;
            rider.SetSeatedState(true);
            UpdateRider();
            if (_engine != null) _engine.Play();
            enabled = true;
        }

        public void Release()
        {
            if (_rider != null)
            {
                if (_rider.Humanoid != null) _rider.Humanoid.SetCycling(false);
                _rider.SetSeatedState(false);
            }

            _rider = null;
            _dismountFinished = null;
            _speed = 0f;
            _crankSpinRate = 0f;
            _mountProgress = 1f;
            _lean = 0f;
            _pitch = 0f;
            _smoothedSteer = 0f;
            _accelLean = 0f;
            if (_visual != null) _visual.localRotation = Quaternion.identity;
            if (_fork != null) _fork.localRotation = Quaternion.identity;
            if (_engine != null) _engine.Stop();
            enabled = false;
        }

        public bool TryGetDismount(out Vector3 position)
        {
            if (IsMotorcycle && TryDismountAt(-transform.right, 1.1f, out position)) return true;
            if (TryDismountAt(transform.right, 1.1f, out position)) return true;
            if (TryDismountAt(-transform.right, 1.1f, out position)) return true;
            if (TryDismountAt(-transform.forward, 1.15f, out position)) return true;
            if (TryDismountAt(transform.forward, 1.25f, out position)) return true;
            if (TryDismountAt(transform.right, 1.65f, out position)) return true;
            if (TryDismountAt(-transform.right, 1.65f, out position)) return true;
            // Guaranteed safe fallback right beside bike so player can never get permanently trapped in a corner
            position = transform.position + Vector3.up * 0.25f;
            return true;
        }

        public void Dismount(Vector3 position, Quaternion rotation, Action finished)
        {
            _mountPosition = position;
            _mountRotation = rotation;
            _speed = 0f;
            _dismountFinished = finished;
        }

        private bool TryDismountAt(Vector3 direction, float distance, out Vector3 position)
        {
            position = transform.position + direction * distance;
            if (!Physics.Raycast(position + Vector3.up * 0.85f, Vector3.down, out RaycastHit ground, 1.8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.65f)
            {
                return false;
            }

            position.y = ground.point.y + 0.07f;
            return !Physics.CheckCapsule(position + Vector3.up * 0.36f, position + Vector3.up * 1.44f, 0.27f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private void Update()
        {
            if (_rider == null || InputHub.Instance == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_dismountFinished != null)
            {
                float settle = 1f - Mathf.Exp(-10f * dt);
                _lean = Mathf.Lerp(_lean, 0f, settle);
                _pitch = Mathf.Lerp(_pitch, 0f, settle);
                _smoothedSteer = Mathf.Lerp(_smoothedSteer, 0f, settle);
                _accelLean = Mathf.Lerp(_accelLean, 0f, settle);
                if (_visual != null) _visual.localRotation = Quaternion.Euler(_pitch, 0f, _lean);
                if (_fork != null) _fork.localRotation = Quaternion.Euler(0f, _smoothedSteer * 20f, 0f);
                _mountProgress = Mathf.MoveTowards(_mountProgress, 0f, dt / (IsMotorcycle ? 0.52f : 0.44f));
                UpdateRider();
                if (_mountProgress <= 0f) _dismountFinished.Invoke();
                return;
            }

            if (IsMounting)
            {
                float settle = 1f - Mathf.Exp(-10f * dt);
                _lean = Mathf.Lerp(_lean, 0f, settle);
                _pitch = Mathf.Lerp(_pitch, 0f, settle);
                _smoothedSteer = Mathf.Lerp(_smoothedSteer, 0f, settle);
                if (_visual != null) _visual.localRotation = Quaternion.Euler(_pitch, 0f, _lean);
                if (_fork != null) _fork.localRotation = Quaternion.Euler(0f, _smoothedSteer * 20f, 0f);
                _mountProgress = Mathf.MoveTowards(_mountProgress, 1f, dt / (IsMotorcycle ? 0.58f : 0.46f));
                UpdateRider();
                return;
            }

            InputHub input = InputHub.Instance;
            bool locked = PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked;
            float throttle = locked ? 0f : input.DriveThrottle;
            float brake = locked || input.HandbrakeHeld ? 1f : input.DriveBrake;
            float steer = locked ? 0f : input.DriveSteer;
            float topSpeed = MaxSpeedKmh / 3.6f;

            // Support gentle foot-push reverse when stopped and holding Brake so bikes never stay nose-pinned at walls
            bool wantReverse = !locked && !input.HandbrakeHeld && brake > 0.05f && throttle <= 0.01f && _speed <= 0.12f;
            float targetSpeed = wantReverse ? -1.75f * brake : (brake > 0f ? 0f : throttle * topSpeed);
            float rate = wantReverse ? 4.2f :
                (brake > 0f ? (IsMotorcycle ? 15f : 10f) * brake :
                (throttle > 0f ? (IsMotorcycle ? 6.4f : 3.5f) : (IsMotorcycle ? 2.2f : 1.0f)));
            float prevSpeed = _speed;
            _speed = Mathf.MoveTowards(_speed, targetSpeed, rate * dt);

            float absSpeed = Mathf.Abs(_speed);
            float speedFactor = Mathf.InverseLerp(0.10f, 1.9f, absSpeed);
            float turnRate = Mathf.Lerp(98f, IsMotorcycle ? 33f : 45f, absSpeed / topSpeed);
            float steerFollow = (steer * _smoothedSteer < -0.01f) ? 18f : 12.5f;
            _smoothedSteer = Mathf.Lerp(_smoothedSteer, steer, 1f - Mathf.Exp(-steerFollow * dt));
            float yawSign = _speed < -0.05f ? -1f : 1f;
            // Drive yaw from _smoothedSteer so lean, handlebars, and turn arc stay 100% synchronized
            transform.Rotate(0f, _smoothedSteer * speedFactor * turnRate * yawSign * dt, 0f, Space.World);
            _fallSpeed = _body.isGrounded ? -2f : Mathf.Max(-15f, _fallSpeed - 16f * dt);
            Vector3 before = transform.position;
            CollisionFlags collisions = _body.Move((transform.forward * _speed + Vector3.up * _fallSpeed) * dt);
            if ((collisions & CollisionFlags.Sides) != 0 && _speed > 0f)
            {
                _speed = Mathf.MoveTowards(_speed, Mathf.Min(_speed, 3.2f), 22f * dt);
            }

            // Real-world deep water guard: prevent riding a bicycle/motorcycle underwater on pool/lake floor!
            if (WaterZone.TrySampleWater(transform.position, out float waterSurfY, out _) && (waterSurfY - transform.position.y) > 0.65f)
            {
                OurWorld.Core.VisualEffectsManager.SpawnWaterSplash(new Vector3(transform.position.x, waterSurfY, transform.position.z), bigDive: true);
                OurWorld.Core.GameFeelAudio.PlayWaterSplash();
                _speed = 0f;
                PlayerModeController mode = PlayerModeController.Instance;
                if (mode != null && mode.CurrentBicycle == this)
                {
                    Vector3 swimPos = transform.position + Vector3.up * 0.2f;
                    mode.SetOnFootMode(snapCamera: false);
                    mode.FootController?.TeleportTo(swimPos, transform.rotation);
                    bool en = OurWorld.Core.LocalizationService.IsEnglish;
                    mode.RaiseStatusMessage(en
                        ? (IsMotorcycle ? "🌊 Motorcycle stalled in deep water — stepped off to swim!" : "🌊 Too deep to cycle — hopped off into the water!")
                        : (IsMotorcycle ? "🌊 Motosiklet derin suda stop etti — yüzmek için indin!" : "🌊 Su bisiklet için çok derin — yüzmeye geçtin!"));
                    return;
                }
            }

            // Expanded Sultanbeyli world bounds & void-fall safety net (matches PlayerFootController & SedanVehicleController)
            Vector3 curPos = transform.position;
            bool outOfBounds = curPos.x < -418f || curPos.x > 418f || curPos.z < -418f || curPos.z > 418f;
            if (curPos.y < -3.5f || outOfBounds)
            {
                bool wasCc = _body.enabled;
                _body.enabled = false;
                transform.position = new Vector3(
                    Mathf.Clamp(curPos.x, -414f, 414f),
                    curPos.y < -3.5f ? 0.30f : curPos.y,
                    Mathf.Clamp(curPos.z, -414f, 414f));
                _body.enabled = wasCc;
                _speed = 0f;
                if (curPos.y < -3.5f) _fallSpeed = 0f;
            }

            Vector3 travel = transform.position - before;
            travel.y = 0f;
            float signedTravel = Vector3.Dot(travel, transform.forward);
            float tireRadius = IsMotorcycle ? 0.36f : WheelRadius;
            float wheelDegrees = signedTravel / tireRadius * Mathf.Rad2Deg;
            _wheelAngle = Mathf.Repeat(_wheelAngle + wheelDegrees, 360f);
            if (_crank != null)
            {
                // Cranks are geared to the rear wheel: derive cadence from the distance actually travelled so
                // pedals never spin while the bike is pinned against a wall, and always match the wheel speed.
                // Coasting / braking freewheels (cranks stop) like a real bicycle.
                bool pedaling = throttle > 0.02f && brake <= 0.01f && signedTravel > 0.0001f;
                float targetCrankRate = pedaling ? (wheelDegrees / dt) / PedalGearRatio : 0f;
                _crankSpinRate = Mathf.Lerp(_crankSpinRate, targetCrankRate, 1f - Mathf.Exp(-7f * dt));
                if (_crankSpinRate > 2f)
                {
                    _crankAngle = Mathf.Repeat(_crankAngle + _crankSpinRate * dt, 360f);
                }
            }

            float accelRaw = (_speed - prevSpeed) / Mathf.Max(0.005f, dt);
            float targetPitch = brake > 0.05f && _speed > 0.4f
                ? Mathf.Clamp((IsMotorcycle ? 4.2f : 2.8f) * brake, 0f, 4.5f)
                : Mathf.Clamp(-accelRaw * (IsMotorcycle ? 0.42f : 0.25f), -2.6f, 3.2f);
            _pitch = Mathf.Lerp(_pitch, targetPitch, 1f - Mathf.Exp(-9f * dt));
            float targetAccelLean = brake > 0.05f && _speed > 0.4f
                ? 0.65f * brake
                : Mathf.Clamp(-accelRaw * 0.14f, -0.55f, 0.70f);
            _accelLean = Mathf.Lerp(_accelLean, targetAccelLean, 1f - Mathf.Exp(-8f * dt));

            float leanDir = _speed >= 0f ? 1f : 0f;
            _lean = Mathf.Lerp(_lean, -_smoothedSteer * (IsMotorcycle ? 26f : 15f) * speedFactor * leanDir, 1f - Mathf.Exp(-8.5f * dt));
            _visual.localRotation = Quaternion.Euler(_pitch, 0f, _lean);
            _fork.localRotation = Quaternion.Euler(0f, _smoothedSteer * Mathf.Lerp(23f, 12f, IsMotorcycle ? absSpeed / topSpeed : 0.25f * absSpeed / topSpeed), 0f);
            _frontWheel.localRotation = Quaternion.Euler(_wheelAngle, 0f, 0f);
            _rearWheel.localRotation = _frontWheel.localRotation;
            if (_crank != null) _crank.localRotation = Quaternion.Euler(_crankAngle, 0f, 0f);
            if (_engine != null)
            {
                _engine.pitch = Mathf.Lerp(0.75f, 1.75f, absSpeed / topSpeed) + throttle * 0.28f;
                _engine.volume = Mathf.Lerp(0.14f, 0.29f, throttle);
            }
            UpdateRider();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit == null || hit.collider == null) return;
            float absSpd = Mathf.Abs(_speed);
            if (absSpd < 0.8f) return;

            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.forward;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude > 0.001f) pushDir.Normalize();

            // Physically push dynamic street props on bike/motorcycle contact (no double impulse!)
            DynamicWorldProp dynProp = hit.collider.GetComponentInParent<DynamicWorldProp>();
            if (dynProp != null)
            {
                if (dynProp.Body != null && dynProp.Body.isKinematic)
                {
                    dynProp.ApplyNudge(pushDir * Mathf.Clamp(absSpd * 1.15f, 1.5f, 7.5f), hit.point);
                }
                else if (dynProp.Body != null)
                {
                    dynProp.Body.WakeUp();
                    dynProp.Body.AddForceAtPosition(pushDir * (absSpd * Mathf.Clamp(dynProp.MassKg * 0.42f, 5f, 24f)), hit.point, ForceMode.Impulse);
                }
            }
            else
            {
                Rigidbody rb = hit.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic && rb.mass <= 150f)
                {
                    rb.WakeUp();
                    rb.AddForceAtPosition(pushDir * (absSpd * Mathf.Clamp(rb.mass * 0.45f, 6f, 28f)), hit.point, ForceMode.Impulse);
                }
            }

            if (absSpd < 2.2f) return;
            float kmh = absSpd * 3.6f;
            Vector3 bikeVel = transform.forward * _speed;

            TownPedestrian ped = hit.collider.GetComponentInParent<TownPedestrian>();
            if (ped != null)
            {
                if (kmh >= 16f) ped.OnHitByVehicle(transform.position, bikeVel, kmh * 0.85f);
                else ped.OnBumpedByPlayer(transform.position, bikeVel);
                return;
            }

            LivingNpcColliderBridge living = hit.collider.GetComponentInParent<LivingNpcColliderBridge>();
            if (living != null)
            {
                if (kmh >= 16f) living.NotifyHitByVehicle(transform.position, bikeVel, kmh * 0.85f);
                else living.NotifyBumpedByPlayer(transform.position, bikeVel);
            }
        }

        private void UpdateRider()
        {
            // The rider shares the bicycle's lean and yaw while the normal foot collider is disabled.
            float mount = Mathf.SmoothStep(0f, 1f, _mountProgress);
            Vector3 position = Vector3.Lerp(_mountPosition, _visual.position, mount);
            position.y += Mathf.Sin(mount * Mathf.PI) * 0.14f;
            _rider.transform.SetPositionAndRotation(position, Quaternion.Slerp(_mountRotation, _visual.rotation, mount));
            if (_rider.Humanoid == null) return;

            if (IsMotorcycle)
            {
                float footPeg = Mathf.SmoothStep(0f, 1f, _speed / 2.2f);
                Vector3 leftStand = transform.TransformPoint(new Vector3(-0.40f, 0.015f, -0.14f));
                if (footPeg < 0.98f && Physics.Raycast(leftStand + Vector3.up * 0.65f, Vector3.down, out RaycastHit gHit, 1.4f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    leftStand.y = gHit.point.y + 0.015f;
                }
                Vector3 leftPeg = _visual.TransformPoint(new Vector3(-0.25f, 0.31f, -0.10f));
                _rider.Humanoid.SetMotorcyclePose(
                    Vector3.Lerp(leftStand, leftPeg, footPeg),
                    _visual.TransformPoint(new Vector3(0.25f, 0.31f, -0.10f)),
                    _fork.TransformPoint(new Vector3(-0.33f, 0.70f, -0.38f)),
                    _fork.TransformPoint(new Vector3(0.33f, 0.70f, -0.38f)), mount, footPeg, _smoothedSteer, _accelLean);
            }
            else
            {
                float footPeg = Mathf.SmoothStep(0f, 1f, _speed / 0.85f);
                Vector3 leftStand = transform.TransformPoint(new Vector3(-0.32f, 0.015f, -0.10f));
                if (footPeg < 0.98f && Physics.Raycast(leftStand + Vector3.up * 0.65f, Vector3.down, out RaycastHit gHit, 1.4f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    leftStand.y = gHit.point.y + 0.015f;
                }
                Vector3 leftPedal = _crank.TransformPoint(new Vector3(-0.16f, 0f, -CrankRadius));
                Vector3 rightPedal = _crank.TransformPoint(new Vector3(0.16f, 0f, CrankRadius));
                Vector3 leftGrip = _fork.TransformPoint(new Vector3(-0.24f, 0.69f, -0.18f));
                Vector3 rightGrip = _fork.TransformPoint(new Vector3(0.24f, 0.69f, -0.18f));
                _rider.Humanoid.SetCycling(true, Vector3.Lerp(leftStand, leftPedal, footPeg), rightPedal, leftGrip, rightGrip,
                    mount, footPeg, _smoothedSteer, _accelLean);
            }
        }
    }
}
