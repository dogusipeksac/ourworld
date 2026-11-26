using System.Collections;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Interactive Turkish mahalle street football (Sokak Futbolu):
    /// - Walk into the ball to dribble smoothly with realistic roll & bounce.
    /// - Press E ('Şut Çek') or punch/kick near the ball to launch a curved power shot!
    /// - Score into any FootballGoal trigger for GOOOOL! fanfare, golden burst & +35 ₺ reward!
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class StreetFootball : MonoBehaviour
    {
        [SerializeField] private float kickHorizontalPower = 13.5f;
        [SerializeField] private float kickLiftPower = 4.6f;
        [SerializeField] private float dribblePushForce = 5.4f;

        private Rigidbody _rb;
        private Vector3 _spawnPosition;
        private float _lastKickTime = -99f;
        private float _lastDribbleTime = -99f;
        private float _lastGoalCashTime = -99f;
        private bool _goalResetting;
        private bool _kickPending;
        private const float GoalCashCooldownSeconds = 20f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 0.48f;
            _rb.linearDamping = 0.45f;
            _rb.angularDamping = 0.65f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            SphereCollider sc = GetComponent<SphereCollider>();
            PhysicsMaterial bouncy = new PhysicsMaterial("FootballBounce")
            {
                bounciness = 0.68f,
                dynamicFriction = 0.32f,
                staticFriction = 0.35f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            sc.sharedMaterial = bouncy;
        }

        private void Start()
        {
            _spawnPosition = transform.position;
        }

        public void KickFromPlayer(PlayerModeController mode, float powerMultiplier = 1f)
        {
            if (mode == null || _rb == null || _kickPending || Time.time - _lastKickTime < 0.25f) return;
            _lastKickTime = Time.time;

            if (mode.FootController != null && mode.FootController.Humanoid != null)
            {
                mode.FootController.Humanoid.PlaySportAnim(SportAnimKind.SoccerKick, 0.50f);
                StartCoroutine(DelayedKickImpulseRoutine(mode.FootController, powerMultiplier));
                return;
            }

            Vector3 dir = transform.position - mode.ActiveWorldPosition;
            ApplyKickImpulse(dir, powerMultiplier);
        }

        private IEnumerator DelayedKickImpulseRoutine(PlayerFootController foot, float powerMultiplier)
        {
            _kickPending = true;
            const float contactDelay = 0.17f; // matches SoccerKick right-foot strike peak
            float elapsed = 0f;
            while (elapsed < contactDelay)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                if (foot != null)
                {
                    Vector3 toBall = transform.position - foot.transform.position;
                    toBall.y = 0f;
                    if (toBall.sqrMagnitude > 0.04f && toBall.sqrMagnitude < 9f)
                    {
                        Quaternion face = Quaternion.LookRotation(toBall.normalized, Vector3.up);
                        foot.transform.rotation = Quaternion.Slerp(foot.transform.rotation, face, 1f - Mathf.Exp(-14f * dt));
                    }
                }
                yield return null;
            }

            Vector3 dir = foot != null ? foot.transform.forward : Vector3.forward;
            ApplyKickImpulse(dir, powerMultiplier);
            _kickPending = false;
        }

        private void ApplyKickImpulse(Vector3 dir, float powerMultiplier)
        {
            if (_rb == null) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 impulse = dir * (kickHorizontalPower * powerMultiplier) + Vector3.up * (kickLiftPower * powerMultiplier);
            _rb.linearVelocity = Vector3.zero;
            _rb.AddForce(impulse, ForceMode.VelocityChange);
            _rb.AddTorque(Vector3.Cross(Vector3.up, dir) * 14f, ForceMode.VelocityChange);

            SportsArenaAtmosphere.PlayLeatherKick(Mathf.Clamp(0.45f * powerMultiplier, 0.32f, 0.75f));
            SportsArenaAtmosphere.SpawnTurfKickPuff(transform.position, heavy: powerMultiplier >= 1.05f);
            GameFeelAudio.SpawnBurst(transform.position, new Color(0.96f, 0.96f, 0.96f), count: 6, spread: 0.35f);
        }

        public void NotifyScored(FootballGoal goal)
        {
            if (_goalResetting) return;
            _goalResetting = true;
            StartCoroutine(GoalCelebrationRoutine(goal));
        }

        private IEnumerator GoalCelebrationRoutine(FootballGoal goal)
        {
            GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
            GameFeelAudio.PlayCrowdCheer(bigCheer: true);
            GameFeelAudio.PlayMilestoneFanfare();
            Vector3 burstPos = goal != null ? goal.transform.position + Vector3.up * 1.2f : transform.position + Vector3.up * 1.0f;
            SportsArenaAtmosphere.CelebrateEvent(burstPos, "football", playerSide: true, bigPlay: true);
            GameFeelAudio.SpawnBurst(burstPos, new Color(1.0f, 0.84f, 0.22f), count: 18, spread: 1.1f);

            int totalGoals = 1;
            bool paidCash = Time.time - _lastGoalCashTime >= GoalCashCooldownSeconds;
            if (paidCash) _lastGoalCashTime = Time.time;

            if (SaveManager.Instance != null)
            {
                if (paidCash) SaveManager.Instance.AddMoney(35);
                if (SaveManager.Instance.Data != null)
                {
                    SaveManager.Instance.Data.footballGoalsScored++;
                    totalGoals = SaveManager.Instance.Data.footballGoalsScored;
                    if (!paidCash) SaveManager.Instance.Save();
                }
            }

            if (PlayerModeController.Instance != null)
            {
                bool isEnGoal = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                string cashPart = paidCash ? " (+35 ₺ · " : " (";
                PlayerModeController.Instance.RaiseStatusMessage(isEnGoal
                    ? $"GOAAAL!! Brilliant street match goal!{cashPart}Total Goals: {totalGoals})"
                    : $"GOOOOL!! Mahalle maçında şahane gol!{cashPart}Toplam Gol: {totalGoals})");
            }

            yield return new WaitForSeconds(2.4f);

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                transform.position = _spawnPosition + Vector3.up * 0.2f;
            }
            _goalResetting = false;
        }

        private float _lastBallIncidentTime = -99f;
        private bool _wasInWater;

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.contactCount == 0 || Time.time - _lastBallIncidentTime < 0.85f) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 4.8f) return;

            Collider other = collision.collider;
            if (other == null) return;
            ContactPoint cp = collision.GetContact(0);
            bool en = LocalizationService.IsEnglish;

            // Classic Istanbul street football detail: blasting the ball into a parked car sets off its alarm!
            OurWorld.Vehicle.SedanVehicleController car = other.GetComponentInParent<OurWorld.Vehicle.SedanVehicleController>();
            if (car != null && !car.IsPlayerDriving && speed >= 5.5f)
            {
                _lastBallIncidentTime = Time.time;
                car.TriggerCarAlarm(6.5f);
                NeighborhoodConsequenceSystem.EnsureInstance().PlayMetalClang(0.54f);
                VisualEffectsManager.SpawnCombatImpact(cp.point, cp.normal, false, new Color(1.0f, 0.82f, 0.30f));
                PlayerModeController.Instance?.RaiseStatusMessage(en
                    ? "⚽ Oops! The football hit a parked car — its alarm went off!"
                    : "⚽ Eyvah! Top park halindeki arabaya geldi — alarm çalmaya başladı!");
                return;
            }

            // Hitting a passing neighbor with a kicked ball startles them with a street reaction
            TownPedestrian ped = other.GetComponentInParent<TownPedestrian>();
            if (ped != null && !ped.IsBusyOrPanicking && speed >= 5.0f)
            {
                _lastBallIncidentTime = Time.time;
                ProceduralHumanoid hum = ped.GetComponentInChildren<ProceduralHumanoid>();
                hum?.PlayStartled(0.95f, transform.position);
                NpcSpeechBubbleManager.Say(
                    ped.transform,
                    ped.CitizenName,
                    en ? "Ouch! Watch where you kick that ball!" : "Ah! Top bana geldi, biraz dikkat etsene!",
                    2.6f,
                    NpcBubbleMood.Alert,
                    2.0f);
                return;
            }

            LivingNpcColliderBridge living = other.GetComponentInParent<LivingNpcColliderBridge>();
            if (living != null && speed >= 5.0f)
            {
                _lastBallIncidentTime = Time.time;
                living.NotifyHitByThrownProp(cp.point, collision.relativeVelocity, en ? "Football" : "Futbol Topu");
                return;
            }

            DynamicWorldProp dynProp = other.GetComponentInParent<DynamicWorldProp>();
            if (dynProp != null && speed >= 4.8f)
            {
                _lastBallIncidentTime = Time.time;
                dynProp.ApplyPunchOrCrashImpulse(-cp.normal * (speed * 2.4f) + Vector3.up * 0.9f, cp.point, speed > 9f);
                return;
            }

            // Shop window / vitrin ball strike -> loud glass thump!
            string nLow = other.gameObject.name.ToLowerInvariant();
            if (speed >= 5.8f && (nLow.Contains("glass") || nLow.Contains("window") || nLow.Contains("vitrin")))
            {
                _lastBallIncidentTime = Time.time;
                NeighborhoodConsequenceSystem.EnsureInstance().PlayGlassClink(0.58f);
                VisualEffectsManager.SpawnCombatImpact(cp.point, cp.normal, false, new Color(0.76f, 0.94f, 1.0f));
            }
        }

        private void Update()
        {
            // Keep ball inside playable neighborhood bounds
            if (transform.position.y < -1.5f || (transform.position - _spawnPosition).sqrMagnitude > 48f * 48f)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                transform.position = _spawnPosition + Vector3.up * 0.2f;
                return;
            }

            // Floating physics & water splash if kicked into the pool or lake!
            if (WaterZone.TrySampleWater(transform.position, out float surfY, out _))
            {
                if (!_wasInWater)
                {
                    _wasInWater = true;
                    VisualEffectsManager.SpawnWaterSplash(new Vector3(transform.position.x, surfY, transform.position.z), bigDive: false);
                    GameFeelAudio.PlayWaterSplash();
                }
                if (transform.position.y < surfY)
                {
                    float depth = surfY - transform.position.y;
                    _rb.AddForce(Vector3.up * (depth * 18f) - _rb.linearVelocity * 1.8f, ForceMode.Acceleration);
                }
            }
            else
            {
                _wasInWater = false;
            }

            // Gentle realistic dribble when player walks or runs right into the ball
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsDriving || mode.FootController == null || Time.time - _lastDribbleTime < 0.14f)
            {
                return;
            }

            Vector3 playerPos = mode.FootController.transform.position;
            Vector3 delta = transform.position - playerPos;
            float horizDistSq = delta.x * delta.x + delta.z * delta.z;
            if (horizDistSq < 0.52f && Mathf.Abs(delta.y) < 0.75f && mode.FootController.CurrentSpeed > 0.35f)
            {
                _lastDribbleTime = Time.time;
                Vector3 pushDir = mode.FootController.transform.forward;
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude > 0.01f) pushDir.Normalize();

                float speedScale = Mathf.Clamp(mode.FootController.CurrentSpeed / 3.4f, 0.5f, 1.55f);
                _rb.AddForce(pushDir * (dribblePushForce * speedScale) + Vector3.up * 0.9f, ForceMode.VelocityChange);
                SportsArenaAtmosphere.PlayLeatherKick(0.24f);
            }
        }
    }

    /// <summary>
    /// Trigger box sitting inside the Istanbul_FootballGoal net to detect goals cleanly!
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FootballGoal : MonoBehaviour
    {
        private void Awake()
        {
            BoxCollider bc = GetComponent<BoxCollider>();
            if (bc != null)
            {
                bc.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            StreetFootball ball = other.GetComponentInParent<StreetFootball>();
            if (ball != null)
            {
                ball.NotifyScored(this);
            }
        }
    }
}
