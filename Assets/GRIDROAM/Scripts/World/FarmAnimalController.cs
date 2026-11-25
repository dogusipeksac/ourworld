using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Expressive, procedural 3D animated farm livestock (Chickens, Cows, Sheep):
    /// - Freely roam and graze inside their sunlit pastures with animated legs, head pecking/grazing,
    ///   tail wagging, wing flaps, and synthesized warm animal sounds.
    /// - Produce collectible fresh Eggs (3D egg appears!), Organic Milk (milking interaction),
    ///   and Natural Wool (sheep's fluffy wool coat visually shrinks when sheared and grows back!).
    /// </summary>
    public sealed class FarmAnimalController : MonoBehaviour
    {
        public static readonly List<FarmAnimalController> AllAnimals = new List<FarmAnimalController>(32);

        private FarmAnimalKind _kind;
        private string _displayName;
        private Vector3 _pastureCenter;
        private Vector2 _pastureHalfSize;
        private InteractableTrigger _trigger;

        private static readonly float[] QuadOffsets = { 0.25f, 0.75f, 0.00f, 0.50f };

        private Transform _visualRoot;
        private Transform _bodyMesh;
        private Transform _headPivot;
        private Transform _tailPivot;
        private Transform _wingL;
        private Transform _wingR;
        private Transform _woolCoat;
        private SkeletalClipPlayer _skeletal;
        private Transform _skelHeadBone;
        private bool _usingSkeletal;
        private float _curHeadLookYaw;
        private float _curHeadLookPitch;
        private bool _wasLookingAtPlayer;
        private readonly List<Transform> _legs = new List<Transform>(4);
        private readonly List<GameObject> _woolPuffs = new List<GameObject>(6);
        private GameObject _readyVisual; // Egg next to chicken, golden milk/wool ready sparkle above cow/sheep

        private Vector3 _targetPos;
        private Vector3 _bodyBaseScale = Vector3.one;
        private float _idleTimer;
        private float _interactionPauseTimer;
        private float _interactFaceTimer;
        private Quaternion _interactFaceTarget = Quaternion.identity;
        private float _currentMoveSpeed;
        private float _walkBlend;
        private float _walkPhase;
        private float _playerReactCooldown;
        private bool _isFleeingPlayer;
        private bool _isApproachingFood;
        private float _produceTimer;
        private float _produceInterval;
        private bool _productReady;
        private float _soundCooldown;
        private float _happyAnimTimer;
        private float _feedEatingTimer;
        private float _woolScaleCurrent = 1f;
        private float _shearVisualBlend;
        private float _tailFlickTimer;
        private float _tailFlickCooldown = 3.5f;
        private float _animSeed;

        private static AudioClip _cluckClip;
        private static AudioClip _mooClip;
        private static AudioClip _baaClip;

        public FarmAnimalKind Kind => _kind;
        public bool IsProductReady => _productReady;

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        public void Initialize(
            FarmAnimalKind kind,
            string displayName,
            Vector3 pastureCenter,
            Vector2 pastureHalfSize,
            InteractableTrigger trigger,
            int variantSeed,
            bool startReady = true)
        {
            _kind = kind;
            _displayName = displayName;
            _pastureCenter = pastureCenter;
            _pastureHalfSize = pastureHalfSize;
            _trigger = trigger;

            _produceInterval = kind switch
            {
                FarmAnimalKind.Chicken => 28f,
                FarmAnimalKind.Cow => 36f,
                _ => 40f
            };
            _productReady = startReady;
            _produceTimer = startReady ? 0f : Random.Range(8f, _produceInterval * 0.6f);
            _animSeed = variantSeed * 1.73f + Random.Range(0f, 6.28f);
            _tailFlickCooldown = Random.Range(2.2f, 5.5f);

            BuildProceduralAnimalMesh(kind, variantSeed);
            PickNextRoamTarget();
            RefreshReadyVisualsAndPrompt();
        }

        private void OnEnable()
        {
            if (!AllAnimals.Contains(this)) AllAnimals.Add(this);
        }

        private void OnDisable()
        {
            AllAnimals.Remove(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_soundCooldown > 0f) _soundCooldown -= dt;
            if (_interactionPauseTimer > 0f) _interactionPauseTimer = Mathf.Max(0f, _interactionPauseTimer - dt);
            if (_playerReactCooldown > 0f) _playerReactCooldown = Mathf.Max(0f, _playerReactCooldown - dt);
            if (_happyAnimTimer > 0f) _happyAnimTimer = Mathf.Max(0f, _happyAnimTimer - dt);
            if (_feedEatingTimer > 0f) _feedEatingTimer = Mathf.Max(0f, _feedEatingTimer - dt);

            // Production timer (eggs, milk, wool regrowing)
            if (!_productReady)
            {
                _produceTimer -= dt;
                if (_produceTimer <= 0f)
                {
                    _productReady = true;
                    RefreshReadyVisualsAndPrompt();
                }
            }

            // Smooth turn during Feed & Pet interaction (never snaps!)
            if (_interactFaceTimer > 0f)
            {
                _interactFaceTimer = Mathf.Max(0f, _interactFaceTimer - dt);
                transform.rotation = Quaternion.Slerp(transform.rotation, _interactFaceTarget, 1f - Mathf.Exp(-7f * dt));
            }

            // Player proximity reaction (step away if player sprints/drives close; come closer for food if calm!)
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null && _interactionPauseTimer <= 0f && _happyAnimTimer <= 0f && _feedEatingTimer <= 0f)
            {
                Vector3 toPlayer = mode.ActiveWorldPosition - transform.position;
                toPlayer.y = 0f;
                float pDist = toPlayer.magnitude;
                bool playerDriving = mode.IsDriving;
                float playerSpeed = playerDriving
                    ? (mode.CurrentVehicle != null ? mode.CurrentVehicle.SpeedKmh / 3.6f : 5f)
                    : (mode.FootController != null ? mode.FootController.CurrentSpeed : 0f);
                bool playerFast = (playerDriving && playerSpeed > 1.2f) || playerSpeed > 3.0f;
                bool shyChickenFlee = _kind == FarmAnimalKind.Chicken && playerSpeed > 2.2f && pDist < 2.8f;

                if (_playerReactCooldown <= 0f && pDist > 0.05f && ((playerFast && pDist < 3.8f) || shyChickenFlee))
                {
                    Vector3 away = -toPlayer / pDist;
                    Vector3 fleeTarget = transform.position + away * Random.Range(2.2f, 3.0f);
                    _targetPos = ClampInsidePasture(fleeTarget);
                    _idleTimer = 0f;
                    _isFleeingPlayer = true;
                    _isApproachingFood = false;
                    _playerReactCooldown = 3.5f;
                }
                else if (_playerReactCooldown <= 0f && !playerDriving && playerSpeed < 2.1f &&
                         pDist >= 2.3f && pDist <= 5.4f && (!_productReady || _kind != FarmAnimalKind.Chicken))
                {
                    Vector3 approachRaw = mode.ActiveWorldPosition - (toPlayer / pDist) * 1.85f;
                    Vector3 approachClamped = ClampInsidePasture(approachRaw);
                    Vector3 moveDelta = approachClamped - transform.position;
                    moveDelta.y = 0f;
                    if (moveDelta.sqrMagnitude >= 0.81f)
                    {
                        _targetPos = approachClamped;
                        _idleTimer = 0f;
                        _isApproachingFood = true;
                        _isFleeingPlayer = false;
                        _playerReactCooldown = Random.Range(9f, 15f);
                    }
                }
            }

            // Roaming & grazing locomotion with smooth turn-before-walk & eased speed
            float baseSpeed = _kind == FarmAnimalKind.Chicken ? 1.15f : (_kind == FarmAnimalKind.Sheep ? 0.95f : 0.85f);
            float maxSpeed = baseSpeed * (_isFleeingPlayer ? 1.38f : (_isApproachingFood ? 0.90f : 1f));
            float desiredMoveSpeed = 0f;
            bool turningInPlace = false;

            if (_interactionPauseTimer > 0f)
            {
                _isFleeingPlayer = false;
                _isApproachingFood = false;
            }
            else if (_idleTimer > 0f)
            {
                _idleTimer -= dt;
                if (_idleTimer <= 0f)
                {
                    _isFleeingPlayer = false;
                    _isApproachingFood = false;
                    PickNextRoamTarget();
                }
            }
            else
            {
                Vector3 to = _targetPos - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < 0.35f)
                {
                    _idleTimer = Random.Range(2.4f, 5.2f);
                    _isFleeingPlayer = false;
                    _isApproachingFood = false;
                }
                else
                {
                    Vector3 dir = to / dist;
                    Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                    float alignDot = Vector3.Dot(transform.forward, dir);
                    float turnRate = _kind == FarmAnimalKind.Chicken ? 7.5f : (_kind == FarmAnimalKind.Sheep ? 5.8f : 4.5f);
                    if (_isFleeingPlayer) turnRate *= 1.35f;
                    else if (alignDot < 0.45f) turnRate *= 1.25f;

                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnRate * dt));

                    float alignGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.90f, alignDot));
                    float arriveGate = Mathf.Clamp01((dist - 0.16f) / 0.65f);
                    desiredMoveSpeed = maxSpeed * alignGate * arriveGate;
                    turningInPlace = alignDot < 0.88f && desiredMoveSpeed < 0.18f;
                }
            }

            _currentMoveSpeed = Mathf.Lerp(_currentMoveSpeed, desiredMoveSpeed, 1f - Mathf.Exp(-6.5f * dt));
            float actualSpeed = 0f;
            if (_currentMoveSpeed > 0.005f)
            {
                Vector3 prevPos = transform.position;
                Vector3 nextPos = ClampInsidePasture(prevPos + transform.forward * (_currentMoveSpeed * dt));
                nextPos.y = prevPos.y;
                transform.position = nextPos;
                Vector3 moved = nextPos - prevPos;
                moved.y = 0f;
                actualSpeed = moved.magnitude / dt;
            }
            bool walking = actualSpeed > 0.08f;

            float tNow = Time.time + _animSeed;

            // Check if player is nearby for curious living head-tracking (with hysteresis to prevent Eating/Idle flicker)
            bool lookingAtPlayer = false;
            float playerYawLocal = 0f;
            float playerPitchLocal = 0f;
            if (mode != null && _happyAnimTimer <= 0f && _feedEatingTimer <= 0f)
            {
                float maxRangeSq = _wasLookingAtPlayer ? 56.25f : 42.25f; // 7.5m exit / 6.5m enter
                float maxYawDeg = _wasLookingAtPlayer ? 96f : 82f;
                Vector3 toP = mode.ActiveWorldPosition + Vector3.up * 1.15f - (transform.position + Vector3.up * (_kind == FarmAnimalKind.Cow ? 1.35f : 0.72f));
                float dSq = toP.sqrMagnitude;
                if (dSq > 0.25f && dSq < maxRangeSq)
                {
                    Vector3 localDir = transform.InverseTransformDirection(toP.normalized);
                    float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(yaw) < maxYawDeg)
                    {
                        lookingAtPlayer = true;
                        playerYawLocal = Mathf.Clamp(yaw, -55f, 55f);
                        playerPitchLocal = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localDir.y, -0.7f, 0.7f)) * Mathf.Rad2Deg, -26f, 22f);
                    }
                }
            }
            _wasLookingAtPlayer = lookingAtPlayer;
            float headTrackRate = 1f - Mathf.Exp(-5.5f * dt);
            _curHeadLookYaw = Mathf.Lerp(_curHeadLookYaw, lookingAtPlayer ? playerYawLocal * 0.55f : 0f, headTrackRate);
            _curHeadLookPitch = Mathf.Lerp(_curHeadLookPitch, lookingAtPlayer ? Mathf.Clamp(playerPitchLocal * 0.45f, -14f, 12f) : 0f, headTrackRate);

            // REAL SKELETAL ANIMATION PATH (PlayableGraph mixing baked Blender / CC0 FBX clips!)
            if (_usingSkeletal && _skeletal != null && _skeletal.HasClips)
            {
                float refWalkSpeed = _kind == FarmAnimalKind.Chicken ? 0.95f : (_kind == FarmAnimalKind.Sheep ? 0.86f : 0.90f);
                if (_happyAnimTimer > 0f)
                {
                    if (!_skeletal.PlayClip("Happy", 0.16f, 1.15f))
                        _skeletal.PlayClip("Idle", 0.16f, 1.2f);
                }
                else if (_feedEatingTimer > 0f)
                {
                    if (!_skeletal.PlayClip("Eating", 0.26f, 1.12f))
                        _skeletal.PlayClip("Idle", 0.26f, 1.0f);
                }
                else if (walking)
                {
                    float walkClipSpd = Mathf.Clamp(actualSpeed / refWalkSpeed, 0.6f, 1.6f);
                    _skeletal.PlayClip("Walk", 0.24f, walkClipSpd);
                }
                else if (turningInPlace)
                {
                    _skeletal.PlayClip("Walk", 0.26f, 0.6f);
                }
                else if (lookingAtPlayer)
                {
                    _skeletal.PlayClip("Idle", 0.34f, 1.0f);
                }
                else
                {
                    float grazeCycle = Mathf.Repeat(tNow * 0.16f, 1f);
                    if (grazeCycle < 0.56f)
                    {
                        if (!_skeletal.PlayClip("Eating", 0.38f, 1.0f))
                            _skeletal.PlayClip("Idle", 0.38f, 1.0f);
                    }
                    else
                    {
                        _skeletal.PlayClip("Idle", 0.38f, 1.0f);
                    }
                }

                // Visual sheared wool feedback on rigged Sheep (live progressive shrink during shearing + regrowth!)
                if (_kind == FarmAnimalKind.Sheep && _visualRoot != null)
                {
                    float targetWool = _productReady
                        ? Mathf.Lerp(1f, 0.88f, _shearVisualBlend)
                        : Mathf.Lerp(0.88f, 0.98f, Mathf.Clamp01(1f - (_produceTimer / Mathf.Max(1f, _produceInterval))));
                    _woolScaleCurrent = Mathf.Lerp(_woolScaleCurrent, targetWool, 1f - Mathf.Exp(-7f * dt));
                    _visualRoot.localScale = new Vector3(_woolScaleCurrent, 1f, _woolScaleCurrent);
                    if (_productReady && _shearVisualBlend <= 0.01f)
                    {
                        // Regrown lush snowy white wool!
                    }
                }

                if (_readyVisual != null && _readyVisual.activeSelf && _kind != FarmAnimalKind.Chicken)
                {
                    float h = _kind == FarmAnimalKind.Cow ? 1.85f : 1.38f;
                    _readyVisual.transform.localPosition = new Vector3(0f, h + Mathf.Sin(Time.time * 3.4f) * 0.07f, 0f);
                }
                return;
            }

            // Smooth procedural walk blend driven by actual ground speed (zero foot sliding!)
            float speedRatio = Mathf.Clamp01(actualSpeed / Mathf.Max(0.2f, baseSpeed));
            _walkBlend = Mathf.Lerp(_walkBlend, walking ? speedRatio : 0f, 1f - Mathf.Exp(-9f * dt));

            // Subtle living ribcage breathing on torso (procedural fallback path)
            if (_bodyMesh != null)
            {
                float breath = Mathf.Sin(tNow * (_kind == FarmAnimalKind.Chicken ? 3.2f : 1.35f)) * 0.022f;
                _bodyMesh.localScale = new Vector3(_bodyBaseScale.x * (1f + breath), _bodyBaseScale.y * (1f + breath * 0.5f), _bodyBaseScale.z);
            }

            if (_visualRoot != null)
            {
                float walkBounce = Mathf.Abs(Mathf.Sin(_walkPhase * 2f)) * (_kind == FarmAnimalKind.Chicken ? 0.028f : 0.032f) * _walkBlend;
                float happyHop = _happyAnimTimer > 0f
                    ? Mathf.Abs(Mathf.Sin(_happyAnimTimer * Mathf.PI * 3.5f)) * (_kind == FarmAnimalKind.Chicken ? 0.24f : 0.14f)
                    : 0f;
                float walkRoll = Mathf.Sin(_walkPhase) * (_kind == FarmAnimalKind.Cow ? 2.6f : 3.8f) * _walkBlend;
                float walkPitch = Mathf.Cos(_walkPhase * 2f) * (_kind == FarmAnimalKind.Chicken ? 3.2f : 1.6f) * _walkBlend;
                _visualRoot.localPosition = new Vector3(0f, walkBounce + happyHop, 0f);
                _visualRoot.localRotation = Quaternion.Slerp(
                    _visualRoot.localRotation,
                    Quaternion.Euler(walkPitch, 0f, walkRoll),
                    1f - Mathf.Exp(-10f * dt));
            }

            if (walking || _walkBlend > 0.02f)
            {
                float phaseScale = Mathf.Clamp(actualSpeed / Mathf.Max(0.2f, baseSpeed), 0.5f, 1.6f);
                if (walking)
                {
                    _walkPhase += dt * (_kind == FarmAnimalKind.Chicken ? 9.2f : (_kind == FarmAnimalKind.Sheep ? 5.6f : 4.2f)) * phaseScale;
                }
                // Authentic 4-beat lateral-sequence quadruped gait: FL=0.25, FR=0.75, BL=0.00, BR=0.50!
                // With asymmetric stance (60% smooth push-back) vs swing (40% quick lift-forward) curve!
                float amp = (_kind == FarmAnimalKind.Chicken ? 32f : (_kind == FarmAnimalKind.Sheep ? 24f : 15f)) * _walkBlend;
                float legSmooth = 1f - Mathf.Exp(-14f * dt);
                for (int i = 0; i < _legs.Count; i++)
                {
                    if (_legs[i] == null) continue;
                    float cyc = Mathf.Repeat(_walkPhase / (Mathf.PI * 2f) + (_legs.Count == 4 ? QuadOffsets[i] : i * 0.5f), 1f);
                    float deg = EvaluateAsymmetricLegAngle(cyc, amp);
                    _legs[i].localRotation = Quaternion.Slerp(_legs[i].localRotation, Quaternion.Euler(deg, 0f, 0f), legSmooth);
                }
                if (walking && _headPivot != null)
                {
                    float pitch;
                    if (_kind == FarmAnimalKind.Chicken)
                    {
                        // Authentic staccato chicken head-stabilization bob!
                        float stepFrac = Mathf.Repeat(_walkPhase / Mathf.PI, 1f);
                        float thrust = stepFrac < 0.28f ? Mathf.SmoothStep(0f, 1f, stepFrac / 0.28f) : Mathf.Lerp(1f, 0f, (stepFrac - 0.28f) / 0.72f);
                        pitch = Mathf.Lerp(-6f, 20f, thrust);
                    }
                    else
                    {
                        pitch = Mathf.Sin(_walkPhase * 2f) * 4.5f * _walkBlend;
                    }
                    float yaw = lookingAtPlayer ? playerYawLocal * 0.65f : 0f;
                    _headPivot.localRotation = Quaternion.Slerp(_headPivot.localRotation, Quaternion.Euler(pitch, yaw, 0f), 1f - Mathf.Exp(-12f * dt));
                }
            }
            else
            {
                for (int i = 0; i < _legs.Count; i++)
                {
                    if (_legs[i] != null)
                    {
                        _legs[i].localRotation = Quaternion.Slerp(_legs[i].localRotation, Quaternion.identity, 1f - Mathf.Exp(-8f * dt));
                    }
                }
            }

            if (!walking && _headPivot != null)
            {
                if (_happyAnimTimer > 0f)
                {
                    float nod = Mathf.Sin(_happyAnimTimer * 18f) * 20f - 8f;
                    _headPivot.localRotation = Quaternion.Slerp(_headPivot.localRotation, Quaternion.Euler(nod, 0f, 0f), 1f - Mathf.Exp(-14f * dt));
                }
                else if (_feedEatingTimer > 0f)
                {
                    float eatPitch = 32f + Mathf.Sin(tNow * 6.4f) * 8f;
                    _headPivot.localRotation = Quaternion.Slerp(_headPivot.localRotation, Quaternion.Euler(eatPitch, 0f, 0f), 1f - Mathf.Exp(-8f * dt));
                }
                else if (lookingAtPlayer)
                {
                    // Curious look straight at approaching player with subtle head tilt!
                    float tilt = Mathf.Sin(tNow * 1.2f) * 7f;
                    _headPivot.localRotation = Quaternion.Slerp(_headPivot.localRotation,
                        Quaternion.Euler(playerPitchLocal, playerYawLocal, tilt), 1f - Mathf.Exp(-5.5f * dt));
                }
                else if (_kind == FarmAnimalKind.Chicken)
                {
                    // Crisp double-peck ground bursts then alert look-around!
                    float cycle = Mathf.Repeat(tNow * 0.45f, 1f);
                    float peck = cycle < 0.14f ? Mathf.Sin((cycle / 0.14f) * Mathf.PI) * 48f
                               : (cycle < 0.28f ? Mathf.Sin(((cycle - 0.14f) / 0.14f) * Mathf.PI) * 44f : 0f);
                    float alertYaw = cycle > 0.35f ? Mathf.Sin(tNow * 1.9f) * 26f : 0f;
                    _headPivot.localRotation = Quaternion.Slerp(_headPivot.localRotation, Quaternion.Euler(peck, alertYaw, 0f), 1f - Mathf.Exp(-16f * dt));
                }
                else
                {
                    // Organic Graze-Bite -> Head-Lift -> Rhythmic Jaw-Chew cycle with eased transitions!
                    float grazeCycle = Mathf.Repeat(tNow * 0.14f, 1f);
                    float downBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(grazeCycle / 0.08f)) *
                                      (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((grazeCycle - 0.54f) / 0.10f)));
                    float grazePitch = 30f + Mathf.Sin(tNow * 3.8f) * 7f;
                    float chewPitch = 2f + Mathf.Sin(tNow * 5.2f) * 3.2f;
                    float pitch = Mathf.Lerp(chewPitch, grazePitch, downBlend);
                    float lookYaw = Mathf.Lerp(Mathf.Sin(tNow * 0.9f) * 22f, Mathf.Sin(tNow * 0.4f) * 8f, downBlend);
                    _headPivot.localRotation = Quaternion.Slerp(_headPivot.localRotation, Quaternion.Euler(pitch, lookYaw, 0f), 1f - Mathf.Exp(-5.5f * dt));
                }
            }

            // Expressive Chicken Wing Flap during happy hops, startled retreat & walking!
            if (_wingL != null && _wingR != null)
            {
                float flap = 0f;
                if (_happyAnimTimer > 0f)
                {
                    flap = 42f + Mathf.Sin(_happyAnimTimer * 34f) * 32f;
                }
                else if (_isFleeingPlayer && walking)
                {
                    flap = 28f + Mathf.Abs(Mathf.Sin(_walkPhase * 1.8f)) * 26f;
                }
                else if (_walkBlend > 0.05f)
                {
                    flap = (6f + Mathf.Abs(Mathf.Sin(_walkPhase)) * 9f) * _walkBlend;
                }
                _wingL.localRotation = Quaternion.Slerp(_wingL.localRotation, Quaternion.Euler(0f, 0f, -flap), 1f - Mathf.Exp(-14f * dt));
                _wingR.localRotation = Quaternion.Slerp(_wingR.localRotation, Quaternion.Euler(0f, 0f,  flap), 1f - Mathf.Exp(-14f * dt));
            }

            // Smooth spring-scaled WoolCoat shearing & gradual regrowth on Blender Sheep!
            if (_woolCoat != null)
            {
                float targetWool = _productReady
                    ? 1f
                    : Mathf.Clamp(1f - (_produceTimer / Mathf.Max(1f, _produceInterval)), 0f, 0.72f) * 0.45f;
                _woolScaleCurrent = Mathf.Lerp(_woolScaleCurrent, targetWool, 1f - Mathf.Exp(-6f * dt));
                bool visible = _woolScaleCurrent > 0.03f;
                if (_woolCoat.gameObject.activeSelf != visible) _woolCoat.gameObject.SetActive(visible);
                if (visible) _woolCoat.localScale = Vector3.one * _woolScaleCurrent;
            }

            // Organic Tail Animation: calm gentle sway punctuated by eased fly-swat tail whips!
            if (_tailPivot != null)
            {
                _tailFlickCooldown -= dt;
                if (_tailFlickCooldown <= 0f)
                {
                    _tailFlickTimer = 0.65f;
                    _tailFlickCooldown = Random.Range(2.8f, 6.2f);
                }
                if (_tailFlickTimer > 0f) _tailFlickTimer = Mathf.Max(0f, _tailFlickTimer - dt);

                float tailYaw;
                if (_happyAnimTimer > 0f)
                {
                    tailYaw = Mathf.Sin(tNow * 18f) * 36f;
                }
                else if (_tailFlickTimer > 0f)
                {
                    float flickEnv = Mathf.Sin(Mathf.Clamp01(_tailFlickTimer / 0.65f) * Mathf.PI);
                    tailYaw = Mathf.Sin(_tailFlickTimer * 24f) * 34f * flickEnv;
                }
                else
                {
                    tailYaw = Mathf.Sin(tNow * 2.1f) * 9f;
                }
                _tailPivot.localRotation = Quaternion.Slerp(_tailPivot.localRotation, Quaternion.Euler(0f, tailYaw, 0f), 1f - Mathf.Exp(-14f * dt));
            }

            if (_readyVisual != null && _readyVisual.activeSelf && _kind != FarmAnimalKind.Chicken)
            {
                float h = _kind == FarmAnimalKind.Cow ? 1.85f : 1.38f;
                _readyVisual.transform.localPosition = new Vector3(0f, h + Mathf.Sin(Time.time * 3.4f) * 0.07f, 0f);
            }
        }

        private void LateUpdate()
        {
            // Additive living head look-at layered smoothly on top of skeletal animation!
            if (_usingSkeletal && _skelHeadBone != null)
            {
                if (Mathf.Abs(_curHeadLookYaw) > 0.5f)
                {
                    _skelHeadBone.Rotate(transform.up, _curHeadLookYaw, Space.World);
                }
                if (Mathf.Abs(_curHeadLookPitch) > 0.5f)
                {
                    _skelHeadBone.Rotate(transform.right, _curHeadLookPitch, Space.World);
                }
            }
        }

        public void PauseForInteraction(float seconds = 3.6f)
        {
            _idleTimer = Mathf.Max(_idleTimer, seconds);
            _interactionPauseTimer = Mathf.Max(_interactionPauseTimer, seconds);
            _isFleeingPlayer = false;
            _isApproachingFood = false;
        }

        private Vector3 ClampInsidePasture(Vector3 worldPos)
        {
            float x = Mathf.Clamp(worldPos.x, _pastureCenter.x - _pastureHalfSize.x, _pastureCenter.x + _pastureHalfSize.x);
            float z = Mathf.Clamp(worldPos.z, _pastureCenter.z - _pastureHalfSize.y, _pastureCenter.z + _pastureHalfSize.y);
            return new Vector3(x, worldPos.y, z);
        }

        private void ApplySheepWoolTint(float sheared01)
        {
            if (_kind != FarmAnimalKind.Sheep || _visualRoot == null) return;
            _shearVisualBlend = Mathf.Clamp01(sheared01);
            Color woolCol = Color.Lerp(new Color(0.96f, 0.94f, 0.89f), new Color(0.82f, 0.74f, 0.62f), _shearVisualBlend);
            OurWorldModelLibrary.TintMaterialByKeyword(_visualRoot.gameObject, "SheepWool", woolCol);
        }

        public void Interact(PlayerModeController mode)
        {
            if (mode == null || PlayerActionAnimator.IsBusyOn(mode)) return;

            // Keep animal calm & stationary during the entire physical interaction (NEVER snap nose-to-nose during flank milking/shearing!)
            PauseForInteraction(4.2f);

            if (_productReady)
            {
                if (_kind == FarmAnimalKind.Chicken)
                {
                    // Target the actual 3D egg resting on the grass so Deniz walks to IT, squats, picks it up & places it in his wicker basket!
                    Transform eggTarget = (_readyVisual != null && _readyVisual.activeSelf) ? _readyVisual.transform : transform;
                    bool groundEggHidden = false;
                    string startMsg = IsEn ? $"Reaching down for {_displayName}'s fresh egg..." : $"{_displayName}'nin yumurtası toplanıyor...";

                    PlayerActionAnimator.Play(
                        mode,
                        eggTarget,
                        PlayerActionKind.CollectEgg,
                        onClimax: () =>
                        {
                            _productReady = false;
                            _produceTimer = _produceInterval;
                            FarmEconomyManager.AddAnimalProduct(_kind, 1);
                            RefreshReadyVisualsAndPrompt();

                            PlayAnimalSound();
                            _happyAnimTimer = 1.35f;
                            GameFeelAudio.PlayCoinChime();
                            GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.7f, new Color(1.0f, 0.94f, 0.72f), count: 12, spread: 0.48f);
                            string bubble = IsEn ? "Bawk-bawk! (+1 Fresh Egg)" : "Gıt-gıt-gıdak! (+1 Köy Yumurtası)";
                            string status = IsEn ? $"Collected +1 Fresh Egg from {_displayName}!" : $"{_displayName} kümesten +1 Taze Köy Yumurtası verdi!";
                            NpcSpeechBubbleManager.Say(transform, _displayName, bubble, 3.0f, NpcBubbleMood.Friendly, 1.35f);
                            mode.RaiseStatusMessage(status);
                        },
                        startStatus: startMsg,
                        onProgress: p =>
                        {
                            // Hide the ground egg at the exact millisecond Deniz's right fingers grasp it!
                            if (!groundEggHidden && p >= 0.45f)
                            {
                                groundEggHidden = true;
                                if (_readyVisual != null) _readyVisual.SetActive(false);
                            }
                        });
                }
                else if (_kind == FarmAnimalKind.Cow)
                {
                    // Cow stands calmly while Deniz approaches her flank, crouches on stool, milks into rising pail, and celebrates on climax!
                    string startMsg = IsEn ? $"Milking {_displayName}..." : $"{_displayName} sağılıyor...";

                    PlayerActionAnimator.Play(
                        mode,
                        transform,
                        PlayerActionKind.MilkCow,
                        onClimax: () =>
                        {
                            _productReady = false;
                            _produceTimer = _produceInterval;
                            FarmEconomyManager.AddAnimalProduct(_kind, 1);
                            RefreshReadyVisualsAndPrompt();

                            PlayAnimalSound();
                            _happyAnimTimer = 1.45f;
                            GameFeelAudio.PlayCoinChime();
                            GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.0f, new Color(0.95f, 0.98f, 1.0f), count: 14, spread: 0.55f);
                            string bubble = IsEn ? "Moooo! (+1 Organic Milk)" : "Möööö! (+1 Taze Süt)";
                            string status = IsEn ? $"Milked {_displayName}: +1 Fresh Organic Milk!" : $"{_displayName} sağıldı: +1 Organik Taze Süt!";
                            NpcSpeechBubbleManager.Say(transform, _displayName, bubble, 3.0f, NpcBubbleMood.Friendly, 2.0f);
                            mode.RaiseStatusMessage(status);
                        },
                        startStatus: startMsg);
                }
                else
                {
                    // Sheep stands calmly while Deniz shears 3 passes along its flank; fleece visibly thins live across every pass!
                    string startMsg = IsEn ? $"Shearing {_displayName}'s wool..." : $"{_displayName}'nin yünü kırpılıyor...";

                    PlayerActionAnimator.Play(
                        mode,
                        transform,
                        PlayerActionKind.ShearSheep,
                        onClimax: () =>
                        {
                            _productReady = false;
                            _produceTimer = _produceInterval;
                            ApplySheepWoolTint(1f);
                            FarmEconomyManager.AddAnimalProduct(_kind, 1);
                            RefreshReadyVisualsAndPrompt();

                            PlayAnimalSound();
                            _happyAnimTimer = 1.40f;
                            GameFeelAudio.PlayCoinChime();
                            GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.9f, new Color(0.96f, 0.94f, 0.88f), count: 14, spread: 0.55f);
                            string bubble = IsEn ? "Baaaa! (+1 Natural Wool)" : "Meeee! (+1 Doğal Yün)";
                            string status = IsEn ? $"Sheared {_displayName}: +1 Natural Wool!" : $"{_displayName} kırpıldı: +1 Doğal Koyun Yünü!";
                            NpcSpeechBubbleManager.Say(transform, _displayName, bubble, 3.0f, NpcBubbleMood.Friendly, 1.35f);
                            mode.RaiseStatusMessage(status);
                        },
                        startStatus: startMsg,
                        onProgress: p =>
                        {
                            ApplySheepWoolTint(p);
                        });
                }
            }
            else
            {
                // Feed & Pet: animal faces gently toward Deniz, eats scattered golden grain, and enjoys affectionate petting!
                Vector3 toP = mode.ActiveWorldPosition - transform.position;
                toP.y = 0f;
                if (toP.sqrMagnitude > 0.04f)
                {
                    _interactFaceTarget = Quaternion.LookRotation(toP.normalized, Vector3.up);
                    _interactFaceTimer = 0.60f;
                }
                string startMsg = IsEn ? $"Feeding & petting {_displayName}..." : $"{_displayName} besleniyor ve seviliyor...";

                PlayerActionAnimator.Play(
                    mode,
                    transform,
                    PlayerActionKind.FeedAndPetAnimal,
                    onClimax: () =>
                    {
                        _produceTimer = Mathf.Max(2f, _produceTimer * 0.45f);
                        PlayAnimalSound();
                        GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.9f, new Color(0.98f, 0.45f, 0.58f), count: 10, spread: 0.45f);
                        string happy = _kind == FarmAnimalKind.Chicken
                            ? (IsEn ? "Cluck! Loves the feed!" : "Gıdak! Yemi çok sevdi!")
                            : (_kind == FarmAnimalKind.Cow
                                ? (IsEn ? "Moo! Happy & fed!" : "Möö! Yemini yedi, çok mutlu!")
                                : (IsEn ? "Baa! Wool growing faster!" : "Mee! Yünü hızla uzuyor!"));
                        NpcSpeechBubbleManager.Say(transform, _displayName, happy, 2.6f, NpcBubbleMood.Friendly, _kind == FarmAnimalKind.Cow ? 2.0f : 1.35f);
                        mode.RaiseStatusMessage(IsEn
                            ? $"Fed & petted {_displayName}! Ready soon."
                            : $"{_displayName} beslendi ve sevildi! Çok yakında hazır.");
                    },
                    startStatus: startMsg,
                    onProgress: p =>
                    {
                        if (p >= 0.45f)
                        {
                            _feedEatingTimer = 3.2f;
                        }
                    });
            }
        }

        private static float EvaluateAsymmetricLegAngle(float cycle01, float maxDeg)
        {
            // Stance (0..0.60): steady rearward sweep supporting body weight;
            // Swing (0.60..1.0): brisk forward snap ready for next footfall!
            if (cycle01 < 0.60f)
            {
                float s = cycle01 / 0.60f;
                return Mathf.Lerp(-maxDeg, maxDeg * 0.92f, s);
            }
            float w = (cycle01 - 0.60f) / 0.40f;
            return Mathf.Lerp(maxDeg * 0.92f, -maxDeg, Mathf.SmoothStep(0f, 1f, w));
        }

        private void PickNextRoamTarget()
        {
            float rx = Random.Range(-_pastureHalfSize.x, _pastureHalfSize.x);
            float rz = Random.Range(-_pastureHalfSize.y, _pastureHalfSize.y);
            _targetPos = new Vector3(_pastureCenter.x + rx, transform.position.y, _pastureCenter.z + rz);
        }

        private void RefreshReadyVisualsAndPrompt()
        {
            if (_readyVisual != null)
            {
                _readyVisual.SetActive(_productReady);
            }

            if (_kind == FarmAnimalKind.Sheep)
            {
                ApplySheepWoolTint(_productReady ? 0f : 1f);
                for (int i = 0; i < _woolPuffs.Count; i++)
                {
                    if (_woolPuffs[i] != null) _woolPuffs[i].SetActive(_productReady);
                }
            }

            if (_trigger == null) return;
            bool isEn = IsEn;
            if (_productReady)
            {
                if (_kind == FarmAnimalKind.Chicken)
                    _trigger.SetCustomPrompt(isEn ? $"Collect Egg ({_displayName})" : $"Yumurta Topla ({_displayName})");
                else if (_kind == FarmAnimalKind.Cow)
                    _trigger.SetCustomPrompt(isEn ? $"Milk Cow ({_displayName})" : $"Süt Sağ ({_displayName})");
                else
                    _trigger.SetCustomPrompt(isEn ? $"Shear Wool ({_displayName})" : $"Yün Kırp ({_displayName})");
            }
            else
            {
                _trigger.SetCustomPrompt(isEn ? $"Feed & Pet ({_displayName})" : $"Yem Ver & Sev ({_displayName})");
            }
        }

        private readonly System.Collections.Generic.List<Material> _runtimeMats =
            new System.Collections.Generic.List<Material>(8);

        private void OnDestroy()
        {
            for (int i = 0; i < _runtimeMats.Count; i++)
            {
                if (_runtimeMats[i] != null) Destroy(_runtimeMats[i]);
            }
            _runtimeMats.Clear();
        }

        private void BuildProceduralAnimalMesh(FarmAnimalKind kind, int seed)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? lit;

            Material Make(Color c, float smooth = 0.22f)
            {
                Material m = new Material(lit);
                _runtimeMats.Add(m);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                return m;
            }

            Material glowMat = new Material(unlit);
            _runtimeMats.Add(glowMat);
            Color glowCol = kind == FarmAnimalKind.Cow ? new Color(0.96f, 0.98f, 1.0f) : new Color(1.0f, 0.90f, 0.42f);
            if (glowMat.HasProperty("_BaseColor")) glowMat.SetColor("_BaseColor", glowCol);
            if (glowMat.HasProperty("_Color")) glowMat.SetColor("_Color", glowCol);

            // Prefer our sculpted commercial-safe Blender FBX models with articulated pivots!
            string fbxName = kind == FarmAnimalKind.Chicken ? "OW_Chicken"
                           : (kind == FarmAnimalKind.Cow ? "OW_Cow" : "OW_Sheep");
            GameObject fbx = OurWorldModelLibrary.InstantiateModel(fbxName, transform, Vector3.zero, Quaternion.identity);
            if (fbx != null)
            {
                _visualRoot = fbx.transform;

                // Activate real baked skeletal animation clips (Idle, Walk, Eating, Happy) if rigged!
                _skeletal = fbx.AddComponent<SkeletalClipPlayer>();
                _usingSkeletal = _skeletal.Initialize(fbxName, "Idle", _animSeed);
                if (_usingSkeletal)
                {
                    _skelHeadBone = _skeletal.FindBone("Head", "Neck3", "Neck");
                }
                else
                {
                    _bodyMesh = OurWorldModelLibrary.FindPart(_visualRoot, "Body");
                    if (_bodyMesh != null) _bodyBaseScale = _bodyMesh.localScale;
                    _headPivot = OurWorldModelLibrary.FindPart(_visualRoot, "HeadPivot");
                    _tailPivot = OurWorldModelLibrary.FindPart(_visualRoot, "TailPivot");
                    _wingL = OurWorldModelLibrary.FindPart(_visualRoot, "Wing_L");
                    _wingR = OurWorldModelLibrary.FindPart(_visualRoot, "Wing_R");
                    _woolCoat = OurWorldModelLibrary.FindPart(_visualRoot, "WoolCoat");

                    string[] legNames = kind == FarmAnimalKind.Chicken
                        ? new[] { "Leg_L", "Leg_R" }
                        : new[] { "Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR" };
                    for (int i = 0; i < legNames.Length; i++)
                    {
                        Transform leg = OurWorldModelLibrary.FindPart(_visualRoot, legNames[i]);
                        if (leg != null) _legs.Add(leg);
                    }
                }

                // Natural coat color variety per animal!
                if (kind == FarmAnimalKind.Chicken)
                {
                    Color fCol = (seed % 3) switch
                    {
                        0 => new Color(0.96f, 0.94f, 0.90f),
                        1 => new Color(0.84f, 0.46f, 0.22f),
                        _ => new Color(0.94f, 0.72f, 0.30f)
                    };
                    OurWorldModelLibrary.TintMaterialByKeyword(fbx, "ChickenFeather", fCol);
                    OurWorldModelLibrary.TintMaterialByKeyword(fbx, "ChickenWing", fCol * 0.86f);
                    _readyVisual = Part(transform, PrimitiveType.Sphere, new Vector3(0.28f, 0.08f, -0.18f), new Vector3(0.14f, 0.19f, 0.14f), Make(new Color(0.98f, 0.94f, 0.84f), 0.35f));
                }
                else if (kind == FarmAnimalKind.Cow)
                {
                    if ((seed % 2) == 1)
                    {
                        // Classic dark Holstein & white blaze coat variant
                        OurWorldModelLibrary.TintMaterialByKeyword(fbx, "CowSpot", new Color(0.14f, 0.14f, 0.15f));
                        OurWorldModelLibrary.TintMaterialByKeyword(fbx, "CowWhite", new Color(0.96f, 0.96f, 0.94f));
                    }
                    _readyVisual = Part(transform, PrimitiveType.Sphere, new Vector3(0f, 1.95f, 0f), new Vector3(0.24f, 0.24f, 0.24f), glowMat);
                    _readyVisual.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                else
                {
                    _woolScaleCurrent = _productReady ? 1f : 0.88f;
                    _readyVisual = Part(transform, PrimitiveType.Sphere, new Vector3(0f, 1.45f, 0f), new Vector3(0.20f, 0.20f, 0.20f), glowMat);
                    _readyVisual.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                return;
            }

            if (kind == FarmAnimalKind.Chicken)
            {
                Color featherCol = (seed % 3) switch
                {
                    0 => new Color(0.96f, 0.95f, 0.92f), // White hen
                    1 => new Color(0.76f, 0.42f, 0.20f), // Red-brown village hen
                    _ => new Color(0.92f, 0.74f, 0.36f)  // Golden hen
                };
                Material bodyMat = Make(featherCol, 0.2f);
                Material combMat = Make(new Color(0.88f, 0.14f, 0.14f), 0.3f);
                Material beakMat = Make(new Color(0.96f, 0.72f, 0.14f), 0.4f);
                Material eyeMat = Make(new Color(0.08f, 0.08f, 0.10f), 0.8f);

                Part(transform, PrimitiveType.Sphere, new Vector3(0f, 0.34f, 0f), new Vector3(0.36f, 0.30f, 0.46f), bodyMat);
                Part(transform, PrimitiveType.Sphere, new Vector3(-0.18f, 0.35f, -0.02f), new Vector3(0.08f, 0.20f, 0.30f), bodyMat);
                Part(transform, PrimitiveType.Sphere, new Vector3( 0.18f, 0.35f, -0.02f), new Vector3(0.08f, 0.20f, 0.30f), bodyMat);

                GameObject hp = new GameObject("HeadPivot");
                hp.transform.SetParent(transform, false);
                hp.transform.localPosition = new Vector3(0f, 0.44f, 0.20f);
                _headPivot = hp.transform;
                Part(_headPivot, PrimitiveType.Sphere, new Vector3(0f, 0.10f, 0.04f), new Vector3(0.20f, 0.20f, 0.22f), bodyMat);
                Part(_headPivot, PrimitiveType.Cube, new Vector3(0f, 0.22f, 0.04f), new Vector3(0.04f, 0.10f, 0.14f), combMat);
                Part(_headPivot, PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.16f), new Vector3(0.06f, 0.05f, 0.10f), beakMat);
                Part(_headPivot, PrimitiveType.Sphere, new Vector3(-0.08f, 0.13f, 0.11f), Vector3.one * 0.04f, eyeMat);
                Part(_headPivot, PrimitiveType.Sphere, new Vector3( 0.08f, 0.13f, 0.11f), Vector3.one * 0.04f, eyeMat);

                GameObject tp = new GameObject("TailPivot");
                tp.transform.SetParent(transform, false);
                tp.transform.localPosition = new Vector3(0f, 0.42f, -0.22f);
                _tailPivot = tp.transform;
                Part(_tailPivot, PrimitiveType.Cube, new Vector3(0f, 0.08f, -0.06f), new Vector3(0.10f, 0.18f, 0.10f), bodyMat).transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);

                _legs.Add(Leg(transform, new Vector3(-0.08f, 0.18f, 0f), new Vector3(0.04f, 0.18f, 0.04f), beakMat));
                _legs.Add(Leg(transform, new Vector3( 0.08f, 0.18f, 0f), new Vector3(0.04f, 0.18f, 0.04f), beakMat));

                // Visible glowing 3D Egg right beside the hen when ready to collect!
                Material eggMat = Make(new Color(0.99f, 0.96f, 0.88f), 0.65f);
                _readyVisual = Part(transform, PrimitiveType.Sphere, new Vector3(0.28f, 0.11f, -0.18f), new Vector3(0.16f, 0.21f, 0.16f), eggMat);
            }
            else if (kind == FarmAnimalKind.Cow)
            {
                bool holstein = (seed % 2) == 0;
                Material hideMat = Make(holstein ? new Color(0.95f, 0.95f, 0.93f) : new Color(0.56f, 0.34f, 0.18f), 0.18f);
                Material spotMat = Make(holstein ? new Color(0.14f, 0.14f, 0.16f) : new Color(0.36f, 0.20f, 0.10f), 0.18f);
                Material snoutMat = Make(new Color(0.92f, 0.74f, 0.72f), 0.35f);
                Material hornMat = Make(new Color(0.88f, 0.84f, 0.72f), 0.4f);
                Material hoofMat = Make(new Color(0.16f, 0.15f, 0.14f), 0.3f);

                // Torso + cute spots + pink udder
                Part(transform, PrimitiveType.Cube, new Vector3(0f, 0.96f, 0f), new Vector3(0.76f, 0.70f, 1.48f), hideMat);
                Part(transform, PrimitiveType.Cube, new Vector3(-0.22f, 1.04f, 0.22f), new Vector3(0.38f, 0.36f, 0.46f), spotMat);
                Part(transform, PrimitiveType.Cube, new Vector3( 0.22f, 0.92f, -0.32f), new Vector3(0.36f, 0.38f, 0.48f), spotMat);
                Part(transform, PrimitiveType.Sphere, new Vector3(0f, 0.58f, -0.25f), new Vector3(0.34f, 0.22f, 0.34f), snoutMat);

                GameObject hp = new GameObject("HeadPivot");
                hp.transform.SetParent(transform, false);
                hp.transform.localPosition = new Vector3(0f, 1.12f, 0.72f);
                _headPivot = hp.transform;
                Part(_headPivot, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0.22f), new Vector3(0.46f, 0.44f, 0.48f), hideMat);
                Part(_headPivot, PrimitiveType.Cube, new Vector3(0f, -0.06f, 0.46f), new Vector3(0.38f, 0.22f, 0.16f), snoutMat);
                Part(_headPivot, PrimitiveType.Cube, new Vector3(-0.28f, 0.14f, 0.16f), new Vector3(0.16f, 0.08f, 0.10f), spotMat);
                Part(_headPivot, PrimitiveType.Cube, new Vector3( 0.28f, 0.14f, 0.16f), new Vector3(0.16f, 0.08f, 0.10f), spotMat);
                Part(_headPivot, PrimitiveType.Cylinder, new Vector3(-0.16f, 0.30f, 0.14f), new Vector3(0.06f, 0.11f, 0.06f), hornMat);
                Part(_headPivot, PrimitiveType.Cylinder, new Vector3( 0.16f, 0.30f, 0.14f), new Vector3(0.06f, 0.11f, 0.06f), hornMat);

                GameObject tp = new GameObject("TailPivot");
                tp.transform.SetParent(transform, false);
                tp.transform.localPosition = new Vector3(0f, 1.08f, -0.74f);
                _tailPivot = tp.transform;
                Part(_tailPivot, PrimitiveType.Cylinder, new Vector3(0f, -0.24f, -0.04f), new Vector3(0.05f, 0.26f, 0.05f), spotMat);

                _legs.Add(Leg(transform, new Vector3(-0.24f, 0.62f,  0.48f), new Vector3(0.16f, 0.62f, 0.16f), hoofMat));
                _legs.Add(Leg(transform, new Vector3( 0.24f, 0.62f,  0.48f), new Vector3(0.16f, 0.62f, 0.16f), hoofMat));
                _legs.Add(Leg(transform, new Vector3(-0.24f, 0.62f, -0.48f), new Vector3(0.16f, 0.62f, 0.16f), hoofMat));
                _legs.Add(Leg(transform, new Vector3( 0.24f, 0.62f, -0.48f), new Vector3(0.16f, 0.62f, 0.16f), hoofMat));

                _readyVisual = Part(transform, PrimitiveType.Sphere, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.24f, glowMat);
            }
            else // Sheep!
            {
                Material woolMat = Make(new Color(0.96f, 0.94f, 0.89f), 0.10f);
                Material shearedMat = Make(new Color(0.84f, 0.78f, 0.68f), 0.15f);
                Material faceMat = Make(new Color(0.18f, 0.17f, 0.18f), 0.25f);

                // Inner sheared body (always visible)
                Part(transform, PrimitiveType.Cube, new Vector3(0f, 0.66f, 0f), new Vector3(0.46f, 0.42f, 0.86f), shearedMat);

                // Fluffy cloud wool puffs (visually hidden right after shearing & regrow!)
                _woolPuffs.Add(Part(transform, PrimitiveType.Sphere, new Vector3(0f, 0.72f, 0f), new Vector3(0.76f, 0.66f, 1.02f), woolMat));
                _woolPuffs.Add(Part(transform, PrimitiveType.Sphere, new Vector3(-0.22f, 0.74f, 0.20f), new Vector3(0.42f, 0.42f, 0.46f), woolMat));
                _woolPuffs.Add(Part(transform, PrimitiveType.Sphere, new Vector3( 0.22f, 0.74f, -0.18f), new Vector3(0.42f, 0.42f, 0.46f), woolMat));
                _woolPuffs.Add(Part(transform, PrimitiveType.Sphere, new Vector3(0f, 0.92f, 0.08f), new Vector3(0.48f, 0.34f, 0.54f), woolMat));

                GameObject hp = new GameObject("HeadPivot");
                hp.transform.SetParent(transform, false);
                hp.transform.localPosition = new Vector3(0f, 0.78f, 0.46f);
                _headPivot = hp.transform;
                Part(_headPivot, PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.14f), new Vector3(0.30f, 0.28f, 0.38f), faceMat);
                Part(_headPivot, PrimitiveType.Cube, new Vector3(-0.18f, 0.06f, 0.08f), new Vector3(0.12f, 0.06f, 0.10f), faceMat);
                Part(_headPivot, PrimitiveType.Cube, new Vector3( 0.18f, 0.06f, 0.08f), new Vector3(0.12f, 0.06f, 0.10f), faceMat);
                _woolPuffs.Add(Part(_headPivot, PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0.08f), new Vector3(0.24f, 0.16f, 0.24f), woolMat));

                GameObject tp = new GameObject("TailPivot");
                tp.transform.SetParent(transform, false);
                tp.transform.localPosition = new Vector3(0f, 0.70f, -0.48f);
                _tailPivot = tp.transform;
                Part(_tailPivot, PrimitiveType.Sphere, new Vector3(0f, -0.02f, -0.06f), Vector3.one * 0.16f, woolMat);

                _legs.Add(Leg(transform, new Vector3(-0.16f, 0.44f,  0.28f), new Vector3(0.10f, 0.44f, 0.10f), faceMat));
                _legs.Add(Leg(transform, new Vector3( 0.16f, 0.44f,  0.28f), new Vector3(0.10f, 0.44f, 0.10f), faceMat));
                _legs.Add(Leg(transform, new Vector3(-0.16f, 0.44f, -0.28f), new Vector3(0.10f, 0.44f, 0.10f), faceMat));
                _legs.Add(Leg(transform, new Vector3( 0.16f, 0.44f, -0.28f), new Vector3(0.10f, 0.44f, 0.10f), faceMat));

                _readyVisual = Part(transform, PrimitiveType.Sphere, new Vector3(0f, 1.38f, 0f), Vector3.one * 0.20f, glowMat);
            }
        }

        private static GameObject Part(Transform parent, PrimitiveType prim, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(prim);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        private static Transform Leg(Transform parent, Vector3 hipPos, Vector3 scale, Material mat)
        {
            GameObject pivot = new GameObject("LegPivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = hipPos;
            Part(pivot.transform, PrimitiveType.Cube, new Vector3(0f, -scale.y * 0.5f, 0f), scale, mat);
            return pivot.transform;
        }

        private void PlayAnimalSound()
        {
            if (_soundCooldown > 0f) return;
            _soundCooldown = 1.6f;

            AudioClip clip = GetOrBuildAnimalClip(_kind);
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position + Vector3.up * 0.8f, 0.55f);
            }
        }

        private static AudioClip GetOrBuildAnimalClip(FarmAnimalKind kind)
        {
            const int sr = 22050;
            if (kind == FarmAnimalKind.Chicken)
            {
                if (_cluckClip != null) return _cluckClip;
                int len = (int)(sr * 0.34f);
                float[] data = new float[len];
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)sr;
                    float burst = (t < 0.12f || (t > 0.18f && t < 0.31f)) ? 1f : 0f;
                    float f = 680f + Mathf.Sin(t * 95f) * 140f;
                    data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * burst * 0.28f * (1f - t / 0.34f);
                }
                _cluckClip = AudioClip.Create("Farm_Cluck", len, 1, sr, false);
                _cluckClip.SetData(data, 0);
                return _cluckClip;
            }
            if (kind == FarmAnimalKind.Cow)
            {
                if (_mooClip != null) return _mooClip;
                int len = (int)(sr * 0.72f);
                float[] data = new float[len];
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)sr;
                    float env = Mathf.Sin(Mathf.PI * (t / 0.72f));
                    float f = 148f - t * 24f + Mathf.Sin(t * 14f) * 4f;
                    float wave = Mathf.Sin(2f * Mathf.PI * f * t) * 0.65f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.35f;
                    data[i] = wave * env * 0.34f;
                }
                _mooClip = AudioClip.Create("Farm_Moo", len, 1, sr, false);
                _mooClip.SetData(data, 0);
                return _mooClip;
            }
            if (_baaClip != null) return _baaClip;
            {
                int len = (int)(sr * 0.54f);
                float[] data = new float[len];
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)sr;
                    float env = Mathf.Sin(Mathf.PI * (t / 0.54f));
                    float vibrato = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 19f * t);
                    float f = 310f - t * 35f;
                    data[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.3f) * env * vibrato * 0.30f;
                }
                _baaClip = AudioClip.Create("Farm_Baa", len, 1, sr, false);
                _baaClip.SetData(data, 0);
                return _baaClip;
            }
        }
    }
}
