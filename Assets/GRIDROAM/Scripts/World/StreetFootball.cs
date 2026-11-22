using System.Collections;
using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
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
        private bool _goalResetting;

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
            if (mode == null || _rb == null || Time.time - _lastKickTime < 0.25f) return;
            _lastKickTime = Time.time;

            Vector3 dir;
            if (mode.FootController != null)
            {
                dir = mode.FootController.transform.forward;
                if (mode.FootController.Humanoid != null)
                {
                    mode.FootController.Humanoid.PlayCombatMove(2); // Front kick animation!
                }
            }
            else
            {
                dir = (transform.position - mode.ActiveWorldPosition);
            }

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 impulse = dir * (kickHorizontalPower * powerMultiplier) + Vector3.up * (kickLiftPower * powerMultiplier);
            _rb.linearVelocity = Vector3.zero;
            _rb.AddForce(impulse, ForceMode.VelocityChange);
            _rb.AddTorque(Vector3.Cross(Vector3.up, dir) * 14f, ForceMode.VelocityChange);

            GameFeelAudio.PlayDoorLatch();
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
            GameFeelAudio.PlayMilestoneFanfare();
            Vector3 burstPos = goal != null ? goal.transform.position + Vector3.up * 1.2f : transform.position + Vector3.up * 1.0f;
            GameFeelAudio.SpawnBurst(burstPos, new Color(1.0f, 0.84f, 0.22f), count: 18, spread: 1.1f);

            int totalGoals = 1;
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.AddMoney(35);
                if (SaveManager.Instance.Data != null)
                {
                    SaveManager.Instance.Data.footballGoalsScored++;
                    totalGoals = SaveManager.Instance.Data.footballGoalsScored;
                }
            }

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage($"GOOOOL!! Mahalle maçında şahane gol! (+35 ₺ · Toplam Gol: {totalGoals})");
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
                GameFeelAudio.PlayUiClick();
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
