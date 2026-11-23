using Gridroam.Core;
using Gridroam.Player;
using System.Collections.Generic;
using UnityEngine;

namespace Gridroam.World
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
        internal static StreetCatController ActivePet { get; private set; }

        private readonly Transform[] _legs = new Transform[4];
        private readonly Vector3[] _legPositions = new Vector3[4];
        private readonly Transform[] _eyes = new Transform[2];
        private readonly Vector3[] _eyeScales = new Vector3[2];
        private Transform _head, _tail;
        private Mesh _tailMesh;
        private Vector3 _homePos, _targetPos;
        private float _stateTimer;
        private float _gaitPhase;
        private float _walkWeight;
        private float _lastEnergyPetTime = -99f;
        private float _petDeadline;
        private float _petStarted = -1f;
        private bool _walking;
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

        private void Start()
        {
            _homePos = transform.position;
            _targetPos = _homePos;
            _stateTimer = Random.Range(1.5f, 4.5f);
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

            if (ActivePet == this)
            {
                if (_petPlayer == null || !_petPlayer.isActiveAndEnabled || _petMode == null || _petMode.IsModalLocked)
                {
                    EndPet();
                }
                else
                {
                    LookToward(_petPlayer.transform.position, 8f, dt);
                    if (_petStarted >= 0f)
                    {
                        float petTime = Time.time - _petStarted;
                        if (!_contactMade && petTime >= PetReachTime)
                        {
                            _contactMade = true;
                            GameFeelAudio.PlayCatMeow();
                            if (Time.time - _lastEnergyPetTime >= 10f)
                            {
                                _lastEnergyPetTime = Time.time;
                                PlayerNeedsController.Instance?.RestoreEnergy(12f);
                                _petMode.RaiseStatusMessage($"{catName} eline başını yaslayıp mırladı. (+12 Enerji)");
                            }
                            else _petMode.RaiseStatusMessage($"{catName} keyifle mırlıyor.");
                        }
                        if (petTime >= PetDuration) EndPet();
                    }
                    AnimateModel(dt);
                    return;
                }
            }

            if (_walking)
            {
                Vector3 direction = Vector3.ProjectOnPlane(_targetPos - transform.position, Vector3.up);
                if (direction.magnitude < 0.15f || _stateTimer <= 0f || !TryStep(direction.normalized, dt))
                {
                    _walking = false;
                    _stateTimer = Random.Range(2.5f, 6f);
                }
            }
            else
            {
                PlayerModeController mode = PlayerModeController.Instance;
                if (mode != null && !mode.IsDriving && (mode.ActiveWorldPosition - transform.position).sqrMagnitude < 9f)
                    LookToward(mode.ActiveWorldPosition, 1.8f, dt);

                if (_stateTimer <= 0f)
                {
                    Vector2 target = Random.insideUnitCircle * wanderRadius;
                    _targetPos = _homePos + new Vector3(target.x, 0f, target.y);
                    _walking = true;
                    _stateTimer = Random.Range(2.2f, 4.5f);
                }
            }
            AnimateModel(dt);
        }

        private bool TryStep(Vector3 direction, float dt)
        {
            LookToward(_targetPos, 8f, dt);
            float alignment = Mathf.Max(0f, Vector3.Dot(transform.forward, direction));
            float distance = walkSpeed * alignment * dt;
            if (distance < 0.0001f) return true;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.17f, 0.08f, transform.forward, out _,
                    distance + 0.23f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;

            Vector3 next = transform.position + transform.forward * distance;
            if (!Physics.Raycast(next + Vector3.up * 0.25f, Vector3.down, out RaycastHit ground, 0.43f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.72f) return false;
            next.y = ground.point.y + 0.016f;
            transform.position = next;
            _gaitPhase = Mathf.Repeat(_gaitPhase + distance / 0.35f, 1f);
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
            _walkWeight = Mathf.MoveTowards(_walkWeight, _walking ? 1f : 0f, 7f * dt);
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
                leg.rotation = transform.rotation * Quaternion.Euler(swing * 12f * _walkWeight, 0f, 0f);
            }

            float pet = _petStarted >= 0f
                ? Mathf.SmoothStep(0f, 1f, (Time.time - _petStarted - 0.30f) / 0.32f) *
                  (1f - Mathf.SmoothStep(0f, 1f, (Time.time - _petStarted - 1.44f) / 0.4f))
                : 0f;
            if (_head != null)
            {
                _head.rotation = transform.rotation * Quaternion.Euler(-pet * 13f + Mathf.Sin(Time.time * 3f) * 3f,
                    pet * Mathf.Sin(Time.time * 7f) * 8f, -pet * 8f);
            }
            if (_tail != null) _tail.rotation = transform.rotation * Quaternion.Euler(-8f * pet, Mathf.Sin(Time.time * 3.6f) * (9f + pet * 14f), 0f);
            float blink = Mathf.Max(pet * 0.83f, Mathf.Clamp01((Mathf.Sin(Time.time * 2.4f) - 0.96f) * 25f));
            for (int i = 0; i < _eyes.Length; i++)
                if (_eyes[i] != null) _eyes[i].localScale = _eyeScales[i] * Mathf.Lerp(1f, 0.33f, blink);
        }

        private void OnDisable()
        {
            if (ActivePet == this) EndPet();
        }

        private void OnDestroy()
        {
            if (_tailMesh != null) Destroy(_tailMesh);
        }
    }
}
