using OurWorld.Core;
using OurWorld.Player;
using System.Collections.Generic;
using UnityEngine;

namespace OurWorld.World
{
    public class StreetCatController : MonoBehaviour
    {
        [SerializeField] private string catName = "Sarman";
        [SerializeField] private Color furColor = new Color(0.86f, 0.56f, 0.28f, 1f);
        [SerializeField] private float wanderRadius = 3.2f;
        [SerializeField] private float walkSpeed = 1.05f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly string[] Paws = { "FL", "FR", "BL", "BR" };
        private static readonly float[] PawPhase = { 0f, 0.5f, 0.73f, 0.23f };
        private const float PetDuration = 1.92f;
        private const float PetReachTime = 0.52f;
        private static readonly List<StreetCatController> AllCats = new List<StreetCatController>(16);
        internal static StreetCatController ActivePet { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCatStatics()
        {
            ActivePet = null;
            AllCats.Clear();
        }

        private readonly Transform[] _legs = new Transform[4];
        private readonly Vector3[] _legPositions = new Vector3[4];
        private readonly Transform[] _eyes = new Transform[2];
        private readonly Vector3[] _eyeScales = new Vector3[2];
        private readonly Transform[] _ears = new Transform[2];
        private readonly Quaternion[] _earBaseRots = { Quaternion.identity, Quaternion.identity };
        private Transform _head, _tail;
        private Mesh _tailMesh;
        private Vector3 _homePos, _targetPos;
        private float _stateTimer;
        private float _gaitPhase;
        private float _walkWeight;
        private float _currentSpeed;
        private float _animSeed;
        private float _petBlend;
        private float _petStrokeSweep;
        private float _cautionBlend;
        private float _curiousBlend;
        private float _reactCooldown;
        private float _lookYawSmoothed;
        private float _lookPitchSmoothed;
        private float _earBackSmoothed;
        private float _earSplaySmoothed;
        private float _blinkSmoothed;
        private Vector3 _headEulerSmoothed;
        private Vector3 _tailEulerSmoothed;
        private float _lastEnergyPetTime = -99f;
        private float _petDeadline;
        private float _petStarted = -1f;
        private bool _walking;
        private bool _retreating;
        private bool _approachingPlayer;
        private bool _isCurious;
        private bool _contactMade;
        private PlayerModeController _petMode;
        private PlayerFootController _petPlayer;

        public void Configure(string displayName, Color coatColor, float radius = 3.2f)
        {
            catName = displayName;
            furColor = coatColor;
            wanderRadius = Mathf.Max(1f, radius);
            ApplyFurColor();
        }

        private float _lastHitTime = -10f;

        private void OnEnable()
        {
            if (!AllCats.Contains(this)) AllCats.Add(this);
        }

        public static bool TryReceivePlayerPunch(Vector3 strikeCenter, float radius, Vector3 attackerPos, Vector3 punchDir, int comboStep)
        {
            StreetCatController best = null;
            float maxR = radius + 0.55f;
            float bestDistSq = maxR * maxR;
            for (int i = 0; i < AllCats.Count; i++)
            {
                StreetCatController c = AllCats[i];
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                if (Time.time - c._lastHitTime < 0.45f) continue;
                Vector3 d = (c.transform.position + Vector3.up * 0.35f) - strikeCenter;
                if (Mathf.Abs(d.y) > 1.45f) continue;
                d.y = 0f;
                float dSq = d.sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = c;
                }
            }
            if (best == null) return false;

            best.OnPunchedByPlayer(attackerPos, punchDir);
            return true;
        }

        private void OnPunchedByPlayer(Vector3 attackerPos, Vector3 punchDir)
        {
            _lastHitTime = Time.time;
            if (ActivePet == this) EndPet();

            NeighborhoodConsequenceSystem.EnsureInstance().PlayCatHiss(0.70f);
            bool en = LocalizationService.IsEnglish;
            bool scratchBack = Random.value < 0.58f;

            if (scratchBack)
            {
                Vector3 toP = attackerPos - transform.position;
                toP.y = 0f;
                if (toP.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toP.normalized, Vector3.up);
                NpcSpeechBubbleManager.Say(transform, catName, en ? "HISSS!! 🐾 (Scratched!)" : "TISSSS!! 🐾 (Tırmaladı!)", 2.8f, NpcBubbleMood.Alert, 0.95f);
                PlayerNeedsController.Instance?.TakeDamage(8f, transform.position, en ? $"{catName} the Cat" : $"{catName} Kedisi", heavyHit: false);
            }
            else
            {
                NpcSpeechBubbleManager.Say(transform, catName, en ? "MREOW!! 🐾" : "MİYAAAV!! 🐾", 2.5f, NpcBubbleMood.Alert, 0.95f);
                PlayerModeController.Instance?.RaiseStatusMessage(en
                    ? $"⚠️ {catName} hissed and bolted away in fear! (-10 Reputation)"
                    : $"⚠️ {catName} tıslayarak korku içinde kaçtı! (-10 Saygınlık)");
            }

            Vector3 away = transform.position - attackerPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = punchDir;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            _targetPos = transform.position + away * 5.2f;
            _targetPos.y = _homePos.y;
            _walking = true;
            _retreating = true;
            _approachingPlayer = false;
            _stateTimer = 4.5f;
            _reactCooldown = 5.0f;

            NeighborhoodConsequenceSystem.EnsureInstance().ReportOffense(
                NeighborhoodOffenseKind.HarmAnimal, transform.position, catName, repPenalty: 10, immediateFineTl: 0, heatDelta: 1.0f);
        }

        private void Start()
        {
            _homePos = transform.position;
            _targetPos = _homePos;
            _stateTimer = Random.Range(1.5f, 4.5f);
            _animSeed = Random.Range(0f, 50f);
            _isCurious = (Mathf.Abs(Mathf.RoundToInt(_homePos.x * 7f + _homePos.z * 13f)) % 3) != 0;
            BindModel();
            ApplyFurColor();
        }

        private void BindModel()
        {
            Transform model = transform.Find(catName + "_Mesh");
            if (model == null) return;
            Transform headMesh = model.Find("Cat_Head");
            if (headMesh == null) return;
            Transform bodyMesh = model.Find("Cat_Body");
            if (bodyMesh != null)
            {
                Vector3 modelForward = headMesh.GetComponent<Renderer>().bounds.center -
                    bodyMesh.GetComponent<Renderer>().bounds.center;
                if (Vector3.Dot(modelForward, transform.forward) < 0f)
                    model.rotation = Quaternion.AngleAxis(180f, transform.up) * model.rotation;
            }

            _head = MakePivot("Head pivot", model, headMesh.GetComponent<Renderer>().bounds.center - transform.up * 0.025f);
            foreach (string part in new[] { "Head", "Muzzle", "Nose", "EyeL", "EyeR", "EarL", "EarR" })
                AttachToPivot(model, _head, "Cat_" + part);
            _eyes[0] = _head.Find("Cat_EyeL");
            _eyes[1] = _head.Find("Cat_EyeR");
            for (int i = 0; i < _eyes.Length; i++)
                if (_eyes[i] != null) _eyeScales[i] = _eyes[i].localScale;
            _ears[0] = _head.Find("Cat_EarL");
            _ears[1] = _head.Find("Cat_EarR");
            for (int i = 0; i < _ears.Length; i++)
                if (_ears[i] != null) _earBaseRots[i] = _ears[i].localRotation;

            Transform tailMesh = model.Find("Cat_Tail_0");
            if (tailMesh != null) BuildTail(model, tailMesh.GetComponent<Renderer>());

            for (int i = 0; i < Paws.Length; i++)
            {
                Transform leg = model.Find("Cat_Leg_" + Paws[i]);
                if (leg == null) continue;
                _legs[i] = MakePivot("Leg " + Paws[i], model, leg.GetComponent<Renderer>().bounds.center + transform.up * 0.076f);
                _legPositions[i] = _legs[i].localPosition;
                leg.SetParent(_legs[i], true);
                AttachToPivot(model, _legs[i], "Cat_Paw_" + Paws[i]);
            }
        }

        private Transform MakePivot(string partName, Transform parent, Vector3 position)
        {
            Transform pivot = new GameObject(partName).transform;
            pivot.SetParent(parent, false);
            pivot.SetPositionAndRotation(position, transform.rotation);
            return pivot;
        }

        private static void AttachToPivot(Transform model, Transform pivot, string part)
        {
            Transform mesh = model.Find(part);
            if (mesh != null) mesh.SetParent(pivot, true);
        }

        private void BuildTail(Transform model, Renderer original)
        {
            Transform oldTip = model.Find("Cat_TailTip");
            Material tip = oldTip != null ? oldTip.GetComponent<Renderer>().sharedMaterial : original.sharedMaterial;
            _tail = MakePivot("Cat_Tail_Rig", transform, transform.TransformPoint(new Vector3(0f, 0.206f, -0.159f)));
            Vector3[] centers =
            {
                Vector3.zero,
                new Vector3(0f, 0.030f, -0.075f),
                new Vector3(0f, 0.096f, -0.118f),
                new Vector3(0f, 0.178f, -0.125f),
                new Vector3(0f, 0.251f, -0.110f),
                new Vector3(0f, 0.287f, -0.077f),
                new Vector3(0f, 0.293f, -0.063f)
            };
            const int sides = 8;
            Vector3[] vertices = new Vector3[centers.Length * sides];
            var furTriangles = new List<int>();
            var tipTriangles = new List<int>();
            for (int ring = 0; ring < centers.Length; ring++)
            {
                Vector3 tangent = (centers[Mathf.Min(ring + 1, centers.Length - 1)] - centers[Mathf.Max(ring - 1, 0)]).normalized;
                Vector3 cross = Vector3.Cross(tangent, Vector3.right).normalized;
                float radius = ring == centers.Length - 1 ? 0.0008f : Mathf.Lerp(0.026f, 0.014f, ring / 5f);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    int entry = ring * sides + side;
                    vertices[entry] = centers[ring] + (Vector3.right * Mathf.Cos(angle) + cross * Mathf.Sin(angle)) * radius;
                    if (ring == 0) continue;
                    int next = ring * sides + (side + 1) % sides;
                    List<int> triangles = ring >= 5 ? tipTriangles : furTriangles;
                    triangles.Add(entry - sides);
                    triangles.Add(next - sides);
                    triangles.Add(next);
                    triangles.Add(entry - sides);
                    triangles.Add(next);
                    triangles.Add(entry);
                }
            }
            _tailMesh = new Mesh { name = "Continuous neighborhood cat tail", vertices = vertices, subMeshCount = 2 };
            _tailMesh.SetTriangles(furTriangles, 0);
            _tailMesh.SetTriangles(tipTriangles, 1);
            _tailMesh.RecalculateNormals();
            _tail.gameObject.AddComponent<MeshFilter>().sharedMesh = _tailMesh;
            _tail.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { original.sharedMaterial, tip };
            for (int i = 0; i < 4; i++)
            {
                Transform part = model.Find("Cat_Tail_" + i);
                if (part != null) part.gameObject.SetActive(false);
            }
            if (oldTip != null) oldTip.gameObject.SetActive(false);
        }

        private void ApplyFurColor()
        {
            var properties = new MaterialPropertyBlock();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                string part = renderer.gameObject.name;
                bool fur = part == "Cat_Body" || part == "Cat_Head" || part.StartsWith("Cat_Ear") ||
                    part.StartsWith("Cat_Leg") || part.StartsWith("Cat_Tail_");
                if (!fur) continue;
                renderer.GetPropertyBlock(properties);
                properties.SetColor(BaseColorId, furColor);
                if (part == "Cat_Tail_Rig") renderer.SetPropertyBlock(properties, 0);
                else renderer.SetPropertyBlock(properties);
            }
        }

        public void Pet(PlayerModeController mode)
        {
            PlayerFootController foot = mode != null ? mode.FootController : null;
            if (foot == null || foot.Humanoid == null || foot.IsSwimming) return;
            if (ActivePet == this) return;

            CancelActivePet();
            ActivePet = this;
            _petMode = mode;
            _petPlayer = foot;
            _petDeadline = Time.time + 2.1f;
            _walking = false;
            _stateTimer = 3.5f;
        }

        internal static void CancelActivePet()
        {
            if (ActivePet != null) ActivePet.EndPet();
        }

        private void EndPet()
        {
            if (ActivePet == this) ActivePet = null;
            _petMode = null;
            _petPlayer = null;
            _petStarted = -1f;
            _contactMade = false;
        }

        internal bool TryGetPetMotion(PlayerFootController player, bool canContinue, Vector3 requested,
            out Vector3 direction, out float speed)
        {
            direction = Vector3.zero;
            speed = 0f;
            if (_petPlayer != player) return false;

            Vector3 offset = Vector3.ProjectOnPlane(transform.position - player.transform.position, Vector3.up);
            direction = offset.normalized;
            bool steeringAway = requested.sqrMagnitude > 0.10f &&
                (_petStarted >= 0f || Vector3.Dot(requested.normalized, direction) < 0.35f);
            if (!canContinue || steeringAway || (_petStarted < 0f && Time.time > _petDeadline))
            {
                EndPet();
                return false;
            }
            if (_petStarted >= 0f) return true;

            const float reach = 0.69f;
            speed = Mathf.Clamp((offset.magnitude - reach + 0.025f) * 4.6f, 0f, 2.0f);
            if (offset.magnitude <= reach && player.CurrentSpeed < 0.28f)
            {
                _petStarted = Time.time;
                speed = 0f;
            }
            return true;
        }

        internal bool TryGetPetPose(ProceduralHumanoid rig, out float crouch, out float weight, out Vector3 hand)
        {
            crouch = weight = 0f;
            hand = Vector3.zero;
            if (_petPlayer == null || _petPlayer.Humanoid != rig || _petStarted < 0f) return false;

            float time = Time.time - _petStarted;
            float rise = Mathf.SmoothStep(0f, 1f, (time - 1.48f) / (PetDuration - 1.48f));
            crouch = Mathf.SmoothStep(0f, 1f, time / PetReachTime) * (1f - rise);
            weight = Mathf.SmoothStep(0f, 1f, (time - 0.12f) / (PetReachTime - 0.12f)) * (1f - rise);
            float stroke = Mathf.Clamp01((time - PetReachTime) / 0.90f);
            float sweep = 0.5f - 0.5f * Mathf.Cos(stroke * 4f * Mathf.PI);
            Vector3 forehead = _head != null ? _head.position + transform.up * 0.102f : transform.TransformPoint(new Vector3(0f, 0.38f, 0.24f));
            hand = forehead - transform.forward * (sweep * 0.145f) - transform.up * (sweep * 0.067f);
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _stateTimer -= dt;
            if (_reactCooldown > 0f) _reactCooldown -= dt;

            if (ActivePet == this)
            {
                if (_petPlayer == null || !_petPlayer.isActiveAndEnabled || _petMode == null || _petMode.IsModalLocked)
                {
                    EndPet();
                }
                else
                {
                    _retreating = false;
                    _approachingPlayer = false;
                    _currentSpeed = Mathf.Lerp(_currentSpeed, 0f, 1f - Mathf.Exp(-10f * dt));
                    LookToward(_petPlayer.transform.position, 8f, dt);
                    if (_petStarted >= 0f)
                    {
                        float petTime = Time.time - _petStarted;
                        if (!_contactMade && petTime >= PetReachTime)
                        {
                            _contactMade = true;
                            GameFeelAudio.PlayCatMeow();
                            bool isEnCat = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
                            if (Time.time - _lastEnergyPetTime >= 10f)
                            {
                                _lastEnergyPetTime = Time.time;
                                PlayerNeedsController.Instance?.RestoreEnergy(12f);
                                _petMode.RaiseStatusMessage(isEnCat ? $"{catName} leaned into your hand and purred. (+12 Energy)" : $"{catName} eline başını yaslayıp mırladı. (+12 Enerji)");
                            }
                            else _petMode.RaiseStatusMessage(isEnCat ? $"{catName} is purring happily." : $"{catName} keyifle mırlıyor.");
                        }
                        if (petTime >= PetDuration) EndPet();
                    }
                    AnimateModel(dt);
                    return;
                }
            }

            // Evaluate approaching player reaction (curious approach vs cautious retreat)
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null)
            {
                Vector3 toPlayer = Vector3.ProjectOnPlane(mode.ActiveWorldPosition - transform.position, Vector3.up);
                float pDist = toPlayer.magnitude;
                bool playerDriving = mode.IsDriving;
                float playerSpeed = playerDriving
                    ? (mode.CurrentVehicle != null ? mode.CurrentVehicle.SpeedKmh / 3.6f : 4f)
                    : (mode.FootController != null ? mode.FootController.CurrentSpeed : 0f);
                bool playerFast = (playerDriving && playerSpeed > 1.2f) || playerSpeed > 3.1f;

                float retreatDist = playerDriving ? 5.6f : (playerFast ? 4.0f : (!_isCurious && playerSpeed > 0.65f ? 2.1f : 0f));
                if (retreatDist > 0f && pDist < retreatDist && pDist > 0.05f)
                {
                    Vector3 away = -toPlayer / pDist;
                    Vector3 desiredRetreat = transform.position + away * 2.6f;
                    Vector3 fromHome = desiredRetreat - _homePos;
                    fromHome.y = 0f;
                    float maxRoam = wanderRadius + 2.4f;
                    if (fromHome.sqrMagnitude > maxRoam * maxRoam)
                    {
                        desiredRetreat = _homePos + fromHome.normalized * maxRoam;
                    }
                    desiredRetreat.y = transform.position.y;
                    _targetPos = desiredRetreat;
                    _retreating = true;
                    _approachingPlayer = false;
                    _walking = true;
                    _stateTimer = Mathf.Max(_stateTimer, 1.35f);
                }
                else if (!_retreating && _isCurious && !playerDriving && !playerFast && _reactCooldown <= 0f &&
                         pDist >= 1.55f && pDist <= 4.3f)
                {
                    Vector3 playerFromHome = mode.ActiveWorldPosition - _homePos;
                    playerFromHome.y = 0f;
                    float maxCuriousRange = wanderRadius + 2.8f;
                    if (playerFromHome.sqrMagnitude <= maxCuriousRange * maxCuriousRange)
                    {
                        Vector3 stopShort = mode.ActiveWorldPosition - (toPlayer / pDist) * 1.15f;
                        stopShort.y = transform.position.y;
                        _targetPos = stopShort;
                        _approachingPlayer = true;
                        _walking = true;
                        _stateTimer = Random.Range(2.4f, 3.6f);
                        _reactCooldown = Random.Range(9f, 14f);
                    }
                }
                else if (_approachingPlayer)
                {
                    if (playerDriving || playerFast || pDist <= 1.25f || pDist > 5.5f)
                    {
                        _walking = false;
                        _approachingPlayer = false;
                        _stateTimer = Random.Range(2.8f, 4.8f);
                    }
                    else
                    {
                        Vector3 stopShort = mode.ActiveWorldPosition - (toPlayer / pDist) * 1.15f;
                        stopShort.y = transform.position.y;
                        _targetPos = stopShort;
                    }
                }
            }

            if (_walking)
            {
                Vector3 direction = Vector3.ProjectOnPlane(_targetPos - transform.position, Vector3.up);
                float dist = direction.magnitude;
                float moveSpeed = walkSpeed * (_retreating ? 1.42f : (_approachingPlayer ? 0.90f : 1f));
                if (dist < 0.16f || _stateTimer <= 0f || !TryStep(direction / Mathf.Max(0.0001f, dist), dist, moveSpeed, dt))
                {
                    _walking = false;
                    _retreating = false;
                    _approachingPlayer = false;
                    _stateTimer = Random.Range(2.5f, 6f);
                }
            }
            else
            {
                _currentSpeed = Mathf.Lerp(_currentSpeed, 0f, 1f - Mathf.Exp(-9f * dt));
                if (mode != null && !mode.IsDriving && (mode.ActiveWorldPosition - transform.position).sqrMagnitude < 12.25f)
                    LookToward(mode.ActiveWorldPosition, 2.2f, dt);

                if (_stateTimer <= 0f)
                {
                    Vector2 target = Random.insideUnitCircle * wanderRadius;
                    _targetPos = _homePos + new Vector3(target.x, 0f, target.y);
                    _walking = true;
                    _retreating = false;
                    _approachingPlayer = false;
                    _stateTimer = Random.Range(2.2f, 4.5f);
                }
            }
            AnimateModel(dt);
        }

        private bool TryStep(Vector3 direction, float remainingDist, float maxSpeed, float dt)
        {
            LookToward(_targetPos, _retreating ? 10.5f : 7.5f, dt);
            float alignment = Vector3.Dot(transform.forward, direction);
            float alignGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.90f, alignment));
            float arriveGate = Mathf.Clamp01(remainingDist / 0.45f);
            float targetSpeed = maxSpeed * alignGate * arriveGate;
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, 1f - Mathf.Exp(-9f * dt));

            float distance = _currentSpeed * dt;
            if (distance < 0.0001f) return true;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.17f, 0.08f, transform.forward, out _,
                    distance + 0.23f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                _currentSpeed = 0f;
                return false;
            }

            Vector3 next = transform.position + transform.forward * distance;
            if (!Physics.Raycast(next + Vector3.up * 0.25f, Vector3.down, out RaycastHit ground, 0.43f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.72f)
            {
                _currentSpeed = 0f;
                return false;
            }
            next.y = ground.point.y + 0.016f;
            transform.position = next;
            _gaitPhase = Mathf.Repeat(_gaitPhase + distance / 0.332f, 1f);
            return true;
        }

        private void LookToward(Vector3 point, float rate, float dt)
        {
            Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.04f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-rate * dt));
        }

        private void AnimateModel(float dt)
        {
            float tNow = Time.time + _animSeed;
            float speedNorm = Mathf.Clamp01(_currentSpeed / Mathf.Max(0.2f, walkSpeed));
            _walkWeight = Mathf.Lerp(_walkWeight, speedNorm, 1f - Mathf.Exp(-10f * dt));

            for (int i = 0; i < _legs.Length; i++)
            {
                Transform leg = _legs[i];
                if (leg == null) continue;
                float phase = Mathf.Repeat(_gaitPhase + PawPhase[i], 1f);
                float swing = Mathf.InverseLerp(0.62f, 1f, phase);
                float placement = phase < 0.62f
                    ? Mathf.Lerp(0.103f, -0.103f, phase / 0.62f) : Mathf.SmoothStep(-0.103f, 0.103f, swing);
                float lift = Mathf.Sin(swing * Mathf.PI) * 0.043f;
                leg.localPosition = _legPositions[i] + leg.parent.InverseTransformVector((transform.forward * placement +
                    transform.up * lift) * _walkWeight);
                Quaternion targetLegRot = transform.rotation * Quaternion.Euler(swing * 12f * _walkWeight, 0f, 0f);
                leg.rotation = Quaternion.Slerp(leg.rotation, targetLegRot, 1f - Mathf.Exp(-16f * dt));
            }

            // Smooth petting blend & stroke phase (fades gently even if pet is cancelled early!)
            float petTarget = 0f;
            float sweepTarget = 0f;
            if (_petStarted >= 0f)
            {
                float petTime = Time.time - _petStarted;
                petTarget = Mathf.SmoothStep(0f, 1f, (petTime - 0.24f) / 0.32f) *
                            (1f - Mathf.SmoothStep(0f, 1f, (petTime - 1.44f) / 0.44f));
                float stroke = Mathf.Clamp01((petTime - PetReachTime) / 0.90f);
                sweepTarget = 0.5f - 0.5f * Mathf.Cos(stroke * 4f * Mathf.PI);
            }
            _petBlend = Mathf.Lerp(_petBlend, petTarget, 1f - Mathf.Exp(-8.5f * dt));
            _petStrokeSweep = Mathf.Lerp(_petStrokeSweep, sweepTarget, 1f - Mathf.Exp(-12f * dt));

            // Smooth player head look-at & curious/cautious emotional blends
            float desiredLookYaw = 0f;
            float desiredLookPitch = 0f;
            bool watchingPlayer = false;
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null && _petBlend < 0.35f)
            {
                Vector3 headOrigin = _head != null ? _head.position : transform.position + Vector3.up * 0.30f;
                Vector3 toEye = (mode.ActiveWorldPosition + Vector3.up * (mode.IsDriving ? 1.15f : 1.35f)) - headOrigin;
                float dSq = toEye.sqrMagnitude;
                if (dSq > 0.09f && dSq < 36f) // Within 6 m
                {
                    Vector3 localDir = transform.InverseTransformDirection(toEye.normalized);
                    float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(yaw) < 115f)
                    {
                        watchingPlayer = true;
                        desiredLookYaw = Mathf.Clamp(yaw, -48f, 48f);
                        desiredLookPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localDir.y, -0.75f, 0.75f)) * Mathf.Rad2Deg, -26f, 14f);
                    }
                }
            }
            float lookRate = 1f - Mathf.Exp(-7f * dt);
            _lookYawSmoothed = Mathf.Lerp(_lookYawSmoothed, desiredLookYaw, lookRate);
            _lookPitchSmoothed = Mathf.Lerp(_lookPitchSmoothed, desiredLookPitch, lookRate);

            _cautionBlend = Mathf.Lerp(_cautionBlend, _retreating ? 1f : 0f, 1f - Mathf.Exp(-6f * dt));
            bool isCuriousNow = !_retreating && (_approachingPlayer || (watchingPlayer && _petBlend < 0.2f));
            _curiousBlend = Mathf.Lerp(_curiousBlend, isCuriousNow ? 1f : 0f, 1f - Mathf.Exp(-5f * dt));

            // Head: idle bob + player look-at + pushing gently UP into the petting hand
            if (_head != null)
            {
                float idlePitch = Mathf.Sin(tNow * 2.4f) * 2.6f;
                float idleYaw = watchingPlayer ? 0f : Mathf.Sin(tNow * 0.85f) * 9f;
                float petPushPitch = -14f - (1f - _petStrokeSweep) * 6.5f;
                float petNuzzleYaw = Mathf.Sin(tNow * 5.2f) * 7.5f;
                float petNuzzleRoll = -8.5f + Mathf.Sin(tNow * 3.1f) * 3.5f;

                Vector3 targetHeadEuler = new Vector3(
                    Mathf.Lerp(idlePitch + _lookPitchSmoothed + _cautionBlend * 6f, petPushPitch, _petBlend),
                    Mathf.Lerp(idleYaw + _lookYawSmoothed, petNuzzleYaw, _petBlend),
                    Mathf.Lerp(_curiousBlend * Mathf.Sin(tNow * 1.3f) * 5.5f, petNuzzleRoll, _petBlend));
                _headEulerSmoothed = Vector3.Lerp(_headEulerSmoothed, targetHeadEuler, 1f - Mathf.Exp(-9f * dt));
                _head.rotation = transform.rotation * Quaternion.Euler(_headEulerSmoothed);
            }

            // Ears: perk forward when curious, relax softly back into hand when petted, flatten back when cautious
            float targetEarBack = _curiousBlend * 6.5f - _petBlend * (11f + _petStrokeSweep * 4.5f) - _cautionBlend * 22f;
            float targetEarSplay = _petBlend * (14f + _petStrokeSweep * 4f) + _cautionBlend * 20f;
            float earBlend = 1f - Mathf.Exp(-8f * dt);
            _earBackSmoothed = Mathf.Lerp(_earBackSmoothed, targetEarBack, earBlend);
            _earSplaySmoothed = Mathf.Lerp(_earSplaySmoothed, targetEarSplay, earBlend);
            for (int i = 0; i < _ears.Length; i++)
            {
                if (_ears[i] == null) continue;
                float sign = i == 0 ? 1f : -1f;
                _ears[i].localRotation = Quaternion.Euler(_earBackSmoothed, 0f, sign * _earSplaySmoothed) * _earBaseRots[i];
            }

            // Tail: happy upright soft purr sway when petted, perky when curious, lowered quick twitch when cautious
            if (_tail != null)
            {
                float tailPitch = _petBlend * 11f + _curiousBlend * 7f - _cautionBlend * 18f;
                float swayFreq = Mathf.Lerp(3.2f, 5.2f, Mathf.Max(_petBlend * 0.5f, _cautionBlend));
                float swayAmp = 8.5f + _petBlend * 13f + _cautionBlend * 11f + _walkWeight * 4f;
                float tailYaw = Mathf.Sin(tNow * swayFreq) * swayAmp;
                float tailRoll = Mathf.Cos(tNow * swayFreq * 0.5f) * (2.5f + _petBlend * 5f);
                Vector3 targetTailEuler = new Vector3(tailPitch, tailYaw, tailRoll);
                _tailEulerSmoothed = Vector3.Lerp(_tailEulerSmoothed, targetTailEuler, 1f - Mathf.Exp(-7.5f * dt));
                _tail.rotation = transform.rotation * Quaternion.Euler(_tailEulerSmoothed);
            }

            // Eyes: desynchronized natural blink + blissful soft squint when petted
            float blinkTarget = Mathf.Max(_petBlend * 0.82f, Mathf.Clamp01((Mathf.Sin(tNow * 2.4f) - 0.96f) * 25f));
            _blinkSmoothed = Mathf.Lerp(_blinkSmoothed, blinkTarget, 1f - Mathf.Exp(-14f * dt));
            for (int i = 0; i < _eyes.Length; i++)
                if (_eyes[i] != null) _eyes[i].localScale = _eyeScales[i] * Mathf.Lerp(1f, 0.33f, _blinkSmoothed);
        }

        private void OnDisable()
        {
            AllCats.Remove(this);
            if (ActivePet == this) EndPet();
        }

        private void OnDestroy()
        {
            if (_tailMesh != null) Destroy(_tailMesh);
        }
    }
}
