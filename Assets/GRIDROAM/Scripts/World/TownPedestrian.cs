using Gridroam.Core;
using Gridroam.Missions;
using Gridroam.Player;
using Gridroam.UI;
using Gridroam.Vehicle;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Living interactive town citizen with sidewalk schedules, bench sitting breaks,
    /// natural street conversations, optional vendor shop trading, car avoidance,
    /// and realistic hit-stagger / flee / pushback reactions when punched by the player.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class TownPedestrian : MonoBehaviour
    {
        [SerializeField] private Vector3[] waypoints;
        [SerializeField] private SeatInteractable[] candidateSeats;
        [SerializeField] private float walkSpeed = 1.3f;
        [SerializeField] private float turnSpeed = 6.5f;
        [SerializeField] private ProceduralHumanoid humanoid;

        [Header("Citizen Identity & Social Interaction")]
        [SerializeField] private string citizenName = "Mahalleli";
        [SerializeField] private string citizenRole = "Çınarlı Sakini";
        [SerializeField] private string vendorShopId = string.Empty;
        [SerializeField] private bool isBrave;
        [SerializeField] private string[] chatLines;

        private static readonly string[] DefaultChatLinesTr =
        {
            "Merhaba! Bugün Çınarlı Kasabası oldukça sakin, değil mi?",
            "Tamirhanedeki Marco işinin ehlidir, aracınla bir derdin olursa ona uğra.",
            "Meydandaki Kerem'in tezgâhında iyi koşu ayakkabıları ve ceketler var.",
            "Kuzey Kargo Deposu akşamüstü çok yoğun oluyor, dikkatli sür.",
            "Çınar Kafe'nin kahvesi yorgunluğu hemen alıyor, tavsiye ederim.",
            "Geçen gece batı sokaklarda garip koyu renkli bir sedan dolaşıyordu."
        };

        private static readonly string[] HitReactionsTr =
        {
            "{0}: Hey! Ne yapıyorsun sen?!",
            "{0}: Delirdin mi, çekil başımdan!",
            "{0}: İmdat! Biri yardım etsin!",
            "{0}: Ne derdin var senin?! Polis çağıracağım!",
            "{0}: Dokunma bana! Uzak dur!"
        };

        private int _waypointIndex;
        private int _waypointStep = 1;
        private int _chatCursor;
        private float _idleTimer;
        private float _midWalkIdleCooldown = 12f;
        private float _currentSpeed;

        // Bench sitting state
        private SeatInteractable _occupiedSeat;
        private Vector3 _preSitPosition;
        private Quaternion _preSitRotation = Quaternion.identity;
        private float _sitTimer;

        // Startled / combat reaction state
        private float _startledCooldown;
        private float _hitCooldown;
        private float _fleeTimer;
        private Vector3 _fleeDirection = Vector3.forward;
        private Vector3 _stumbleVelocity;
        private bool _droppedPocketMoney;

        public void ConfigureCitizen(string displayName, string role, string[] lines, bool brave = false, string shopId = "")
        {
            citizenName = string.IsNullOrEmpty(displayName) ? "Mahalleli" : displayName;
            citizenRole = string.IsNullOrEmpty(role) ? "Çınarlı Sakini" : role;
            chatLines = lines;
            isBrave = brave;
            vendorShopId = shopId ?? string.Empty;
        }

        public void TalkToPlayer(Vector3 playerWorldPos)
        {
            // Stop walking and face the player cleanly
            _fleeTimer = 0f;
            _idleTimer = Mathf.Max(_idleTimer, 3.2f);
            Vector3 toPlayer = playerWorldPos - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f && _occupiedSeat == null)
            {
                transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            }

            if (humanoid != null)
            {
                humanoid.PlayWave();
            }

            if (!string.IsNullOrEmpty(vendorShopId) && ShopMenuPanel.Instance != null)
            {
                ShopMenuPanel.Instance.Open(vendorShopId);
                return;
            }

            string[] pool = (chatLines != null && chatLines.Length > 0) ? chatLines : DefaultChatLinesTr;
            string line = pool[_chatCursor % pool.Length];
            _chatCursor++;

            string speakerHeader = string.IsNullOrEmpty(citizenRole)
                ? citizenName
                : $"{citizenName} ({citizenRole})";

            if (MissionManager.Instance != null && !MissionManager.Instance.IsDialoguePlaying)
            {
                MissionManager.Instance.ShowQuickNpcDialogue(speakerHeader, line);
            }
            else if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage($"{citizenName}: {line}");
            }
        }

        public void OnPunchedByPlayer(Vector3 attackerPos, Vector3 punchDir)
        {
            if (_hitCooldown > 0f) return;
            _hitCooldown = 0.65f;

            ReleaseBenchIfSeated();

            Vector3 knockDir = punchDir;
            knockDir.y = 0f;
            if (knockDir.sqrMagnitude < 0.01f)
            {
                knockDir = transform.position - attackerPos;
                knockDir.y = 0f;
            }
            knockDir = knockDir.sqrMagnitude > 0.001f ? knockDir.normalized : Vector3.forward;

            _stumbleVelocity = knockDir * 5.6f;
            _idleTimer = 0f;

            if (humanoid != null)
            {
                humanoid.PlayHitReact();
            }

            string react = string.Format(HitReactionsTr[Random.Range(0, HitReactionsTr.Length)], citizenName);

            // Occasionally drop loose pocket change once per citizen
            if (!_droppedPocketMoney && string.IsNullOrEmpty(vendorShopId) && Random.value < 0.45f && SaveManager.Instance != null)
            {
                _droppedPocketMoney = true;
                int coins = Random.Range(10, 21);
                SaveManager.Instance.AddMoney(coins);
                react += $" (+{coins} ₺ düştü)";
            }

            if (isBrave)
            {
                // Brave citizen turns and shoves back briefly!
                transform.rotation = Quaternion.LookRotation(-knockDir, Vector3.up);
                _idleTimer = 1.6f;
                if (PlayerNeedsController.Instance != null)
                {
                    PlayerNeedsController.Instance.ConsumeEnergy(6f);
                }
                react = $"{citizenName}: Bana mı vurdun?! Al sana! (-6 Enerji)";
            }
            else
            {
                // Non-brave citizen panics and sprints away from the player
                _fleeTimer = 6.5f;
                _fleeDirection = knockDir;
            }

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(react);
            }
        }

        public void Configure(Vector3 a, Vector3 b, ProceduralHumanoid hum, float speed = 1.3f, bool startHeadingToB = true)
        {
            waypoints = new[] { a, b };
            humanoid = hum;
            walkSpeed = speed;
            _waypointIndex = startHeadingToB ? 1 : 0;
            _waypointStep = startHeadingToB ? 1 : -1;
        }

        public void ConfigureSchedule(
            Vector3[] routePoints,
            SeatInteractable[] nearbySeats,
            ProceduralHumanoid hum,
            float speed = 1.3f,
            int startIndex = 0)
        {
            waypoints = routePoints;
            candidateSeats = nearbySeats;
            humanoid = hum;
            walkSpeed = speed;
            if (waypoints != null && waypoints.Length > 0)
            {
                _waypointIndex = Mathf.Clamp(startIndex, 0, waypoints.Length - 1);
            }
        }

        private void Awake()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (humanoid == null)
            {
                humanoid = GetComponentInChildren<ProceduralHumanoid>();
            }

            _midWalkIdleCooldown = Random.Range(9f, 20f);
        }

        private void OnDisable()
        {
            ReleaseBenchIfSeated();
        }

        private void ReleaseBenchIfSeated()
        {
            if (_occupiedSeat != null)
            {
                _occupiedSeat.SetOccupied(false);
                _occupiedSeat = null;
            }
            if (humanoid != null && humanoid.IsSitting)
            {
                humanoid.SetSitting(false, driving: false);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            if (_startledCooldown > 0f)
            {
                _startledCooldown -= dt;
            }
            if (_hitCooldown > 0f)
            {
                _hitCooldown -= dt;
            }

            // Seated bench break
            if (_occupiedSeat != null)
            {
                _sitTimer -= dt;
                if (_sitTimer <= 0f)
                {
                    _occupiedSeat.SetOccupied(false);
                    _occupiedSeat = null;
                    transform.SetPositionAndRotation(_preSitPosition, _preSitRotation);
                    if (humanoid != null)
                    {
                        humanoid.SetSitting(false, driving: false);
                    }
                    _idleTimer = 0.6f;
                }
                return;
            }

            // Apply knockback / stumble displacement (clamped cleanly inside town bounds)
            if (_stumbleVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 nextPos = transform.position + _stumbleVelocity * dt;
                nextPos.x = Mathf.Clamp(nextPos.x, -96f, 96f);
                nextPos.z = Mathf.Clamp(nextPos.z, -38f, 198f);
                transform.position = nextPos;
                _stumbleVelocity = Vector3.MoveTowards(_stumbleVelocity, Vector3.zero, 11f * dt);
            }

            // Panic flee sprint when punched
            if (_fleeTimer > 0f)
            {
                _fleeTimer -= dt;
                if (_fleeDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion fleeRot = Quaternion.LookRotation(_fleeDirection, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, fleeRot, 1f - Mathf.Exp(-10f * dt));
                    Vector3 fleeNext = transform.position + transform.forward * (4.6f * dt);
                    fleeNext.x = Mathf.Clamp(fleeNext.x, -96f, 96f);
                    fleeNext.z = Mathf.Clamp(fleeNext.z, -38f, 198f);
                    transform.position = fleeNext;
                }
                if (humanoid != null)
                {
                    humanoid.SetLocomotion(4.6f, grounded: true, verticalVelocity: 0f);
                }
                return;
            }

            // Check approaching player vehicle for yield / startled pose
            bool yieldingToCar = EvaluateApproachingCar(dt);

            if (waypoints == null || waypoints.Length < 2)
            {
                if (humanoid != null)
                {
                    humanoid.SetLocomotion(0f, grounded: true, verticalVelocity: 0f);
                }
                return;
            }

            float desiredSpeed = 0f;

            if (yieldingToCar)
            {
                desiredSpeed = 0f;
            }
            else if (_idleTimer > 0f)
            {
                _idleTimer -= dt;
                desiredSpeed = 0f;
            }
            else
            {
                _midWalkIdleCooldown -= dt;
                if (_midWalkIdleCooldown <= 0f)
                {
                    _midWalkIdleCooldown = Random.Range(14f, 28f);
                    _idleTimer = Random.Range(3.0f, 5.5f);
                }
                else
                {
                    Vector3 target = waypoints[_waypointIndex];
                    Vector3 toTarget = target - transform.position;
                    toTarget.y = 0f;
                    float dist = toTarget.magnitude;

                    if (dist < 0.42f)
                    {
                        AdvanceScheduleWaypoint();
                    }
                    else
                    {
                        Vector3 dir = toTarget / dist;
                        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * dt));

                        float facingDot = Mathf.Clamp01(Vector3.Dot(transform.forward, dir));
                        desiredSpeed = walkSpeed * Mathf.Lerp(0.35f, 1f, facingDot);
                        transform.position += transform.forward * (_currentSpeed * dt);
                    }
                }
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, desiredSpeed, 4.8f * dt);

            if (humanoid != null)
            {
                humanoid.SetLocomotion(_currentSpeed, grounded: true, verticalVelocity: 0f);
            }
        }

        private void AdvanceScheduleWaypoint()
        {
            // Occasionally sit on a nearby unoccupied bench for 10-16 s
            if (candidateSeats != null && candidateSeats.Length > 0 && Random.value < 0.35f)
            {
                for (int i = 0; i < candidateSeats.Length; i++)
                {
                    SeatInteractable seat = candidateSeats[i];
                    if (seat == null || seat.IsOccupied) continue;

                    float distSq = (seat.SitPoint.position - transform.position).sqrMagnitude;
                    if (distSq <= 42f)
                    {
                        _preSitPosition = transform.position;
                        _preSitRotation = transform.rotation;
                        _occupiedSeat = seat;
                        _occupiedSeat.SetOccupied(true);
                        _sitTimer = Random.Range(10f, 16f);
                        _currentSpeed = 0f;
                        transform.SetPositionAndRotation(seat.SitPoint.position, seat.SitPoint.rotation);
                        if (humanoid != null)
                        {
                            humanoid.SetLocomotion(0f, true, 0f);
                            humanoid.SetSitting(true, driving: false);
                        }
                        return;
                    }
                }
            }

            // Stop and idle 3-6 s at schedule stops sometimes
            _idleTimer = Random.value < 0.65f ? Random.Range(3.0f, 6.0f) : Random.Range(0.7f, 1.5f);

            if (waypoints.Length == 2)
            {
                _waypointIndex = 1 - _waypointIndex;
            }
            else
            {
                int next = _waypointIndex + _waypointStep;
                if (next >= waypoints.Length)
                {
                    _waypointStep = -1;
                    next = waypoints.Length - 2;
                }
                else if (next < 0)
                {
                    _waypointStep = 1;
                    next = 1;
                }
                _waypointIndex = Mathf.Clamp(next, 0, waypoints.Length - 1);
            }
        }

        private bool EvaluateApproachingCar(float dt)
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || !mode.IsDriving || mode.CurrentVehicle == null)
            {
                return false;
            }

            SedanVehicleController car = mode.CurrentVehicle;
            Vector3 toPed = transform.position - car.transform.position;
            toPed.y = 0f;
            float dist = toPed.magnitude;
            if (dist > 6.5f)
            {
                return false;
            }

            float carKmh = car.SpeedKmh;

            // Startled pose & stumble back if fast car (>15 km/h) comes very close
            if (dist < 2.9f && carKmh > 15f && _startledCooldown <= 0f)
            {
                _startledCooldown = 2.2f;
                _idleTimer = 1.1f;
                Vector3 pushDir = dist > 0.05f ? (toPed / dist) : transform.right;
                pushDir.y = 0f;
                _stumbleVelocity = pushDir.normalized * 2.6f;

                if (humanoid != null)
                {
                    humanoid.PlayStartled(1.15f);
                }
                return true;
            }

            // Step aside / stop if player car is within 4 m in front of pedestrian or approaching them
            Vector3 toCar = -toPed;
            bool inFrontOfPed = dist <= 4.2f && Vector3.Dot(transform.forward, toCar.normalized) > 0.25f;
            bool carHeadingTowardPed = dist <= 4.6f && carKmh > 3f && Vector3.Dot(car.transform.forward, toPed.normalized) > 0.55f;

            if (inFrontOfPed || carHeadingTowardPed)
            {
                Vector3 awayFromLane = dist > 0.05f ? (toPed / dist) : transform.right;
                awayFromLane.y = 0f;
                if (dist < 3.4f)
                {
                    transform.position += awayFromLane.normalized * (0.9f * dt);
                }
                return true;
            }

            return false;
        }
    }
}
