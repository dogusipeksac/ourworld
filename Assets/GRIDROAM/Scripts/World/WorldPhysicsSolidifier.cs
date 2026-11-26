using System;
using System.Collections;
using OurWorld.Core;
using OurWorld.Missions;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;

namespace OurWorld.World
{
    public enum DynamicPropMaterialKind
    {
        Wood = 0,
        Metal = 1,
        Rubber = 2,
        Plastic = 3
    }

    /// <summary>
    /// Turns movable open-world street props (trash cans, wooden crates, oil barrels, tool carts,
    /// old tyres) into real physical dynamic Rigidbodies with overlap-safe initialization,
    /// impact SFX/debris, witness reactions when smashed, and automatic void/water recovery.
    /// </summary>
    public sealed class DynamicWorldProp : MonoBehaviour
    {
        private Rigidbody _rb;
        private DynamicPropMaterialKind _matKind = DynamicPropMaterialKind.Wood;
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private float _massKg = 15f;
        private float _lastImpactSfxTime = -10f;
        private float _displacedTimer;
        private bool _startsKinematicUntilHit;

        public float MassKg => _massKg;
        public Rigidbody Body => _rb;

        public void Configure(float massKg, DynamicPropMaterialKind matKind, bool startKinematicUntilHit)
        {
            _massKg = Mathf.Max(3f, massKg);
            _matKind = matKind;
            _startsKinematicUntilHit = startKinematicUntilHit;
            _homePos = transform.position;
            _homeRot = transform.rotation;

            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();

            _rb.mass = _massKg;
            _rb.linearDamping = matKind == DynamicPropMaterialKind.Rubber ? 0.25f : 0.48f;
            _rb.angularDamping = matKind == DynamicPropMaterialKind.Rubber ? 0.45f : 0.85f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.useGravity = !startKinematicUntilHit;
            _rb.isKinematic = startKinematicUntilHit;
            if (!startKinematicUntilHit)
            {
                _rb.Sleep();
            }
        }

        public void ApplyNudge(Vector3 velocityDelta, Vector3 hitPoint)
        {
            if (_rb == null) return;
            WakeDynamicIfNeeded();
            _rb.WakeUp();
            _rb.AddForceAtPosition(velocityDelta * Mathf.Clamp(_massKg * 0.55f, 4f, 18f), hitPoint, ForceMode.Impulse);
        }

        public void ApplyPunchOrCrashImpulse(Vector3 impulse, Vector3 hitPoint, bool heavyImpact)
        {
            if (_rb == null) return;
            WakeDynamicIfNeeded();
            _rb.WakeUp();
            _rb.AddForceAtPosition(impulse, hitPoint, ForceMode.Impulse);
            PlayImpactFeedback(hitPoint, heavyImpact ? 6.5f : 3.2f);
        }

        private void WakeDynamicIfNeeded()
        {
            if (_rb != null && _rb.isKinematic)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || _rb == null) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 1.75f) return;

            Vector3 hitPt = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            PlayImpactFeedback(hitPt, speed);

            if (speed >= 5.2f)
            {
                Vector3 srcPos = collision.transform != null ? collision.transform.position : hitPt;
                NpcWorldAwareness.NotifyPropImpact(hitPt, srcPos, gameObject.name);
            }
        }

        private void PlayImpactFeedback(Vector3 hitPt, float impactSpeed)
        {
            if (Time.time - _lastImpactSfxTime < 0.22f) return;
            _lastImpactSfxTime = Time.time;

            float vol = Mathf.Clamp(impactSpeed * 0.085f, 0.18f, 0.62f);
            NeighborhoodConsequenceSystem cons = NeighborhoodConsequenceSystem.Instance;
            if (cons != null)
            {
                switch (_matKind)
                {
                    case DynamicPropMaterialKind.Metal:
                        cons.PlayMetalClang(vol);
                        break;
                    case DynamicPropMaterialKind.Wood:
                        cons.PlayWoodThud(vol);
                        break;
                    default:
                        cons.PlayStoneThump(vol * 0.85f);
                        break;
                }
            }

            if (impactSpeed >= 3.2f)
            {
                Color puff = _matKind == DynamicPropMaterialKind.Metal
                    ? new Color(0.75f, 0.78f, 0.82f)
                    : (_matKind == DynamicPropMaterialKind.Wood ? new Color(0.68f, 0.52f, 0.34f) : new Color(0.45f, 0.46f, 0.48f));
                GameFeelAudio.SpawnBurst(hitPt + Vector3.up * 0.08f, puff, count: impactSpeed >= 6f ? 8 : 4, spread: 0.32f);
            }
        }

        private void Update()
        {
            if (_rb == null || _rb.isKinematic) return;

            Vector3 p = transform.position;
            // Immediate recovery if knocked beneath the world or deep into water
            if (p.y < -2.2f || Mathf.Abs(p.x) > 246f || Mathf.Abs(p.z) > 246f)
            {
                ResetToHome();
                return;
            }

            // If far away from spawn and player moved away, gently reset upright so streets stay tidy
            if ((p - _homePos).sqrMagnitude > 28f * 28f)
            {
                _displacedTimer += Time.deltaTime;
                if (_displacedTimer >= 22f)
                {
                    Vector3 playerPos = PlayerModeController.Instance != null
                        ? PlayerModeController.Instance.ActiveWorldPosition
                        : Vector3.zero;
                    if ((playerPos - p).sqrMagnitude > 38f * 38f && (playerPos - _homePos).sqrMagnitude > 32f * 32f)
                    {
                        ResetToHome();
                    }
                }
            }
            else
            {
                _displacedTimer = 0f;
            }
        }

        private void ResetToHome()
        {
            _displacedTimer = 0f;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_homePos, _homeRot);
            if (_startsKinematicUntilHit)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
            }
            else
            {
                _rb.Sleep();
            }
        }
    }

    /// <summary>
    /// Runtime physics & collision integrity auditor that guarantees EVERY solid scene obstacle
    /// has a proper Collider, EVERY movable street prop has an overlap-safe dynamic Rigidbody,
    /// and EVERY moving NPC, animal, and vehicle has a properly configured kinematic Rigidbody
    /// (preventing clipping, tunneling, and static-collider PhysX rebuild stalls).
    /// </summary>
    public sealed class WorldPhysicsSolidifier : MonoBehaviour
    {
        public static WorldPhysicsSolidifier Instance { get; private set; }
        private static readonly Collider[] OverlapBuf = new Collider[12];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            GameObject go = new GameObject("OurWorld_WorldPhysicsSolidifier");
            go.AddComponent<WorldPhysicsSolidifier>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            // Wait 3 frames so IstanbulThemeOverhaul, FarmBuilder, MallBuilder & Populator finish spawning
            yield return null;
            yield return null;
            yield return null;
            RunSolidificationPass();

            // Second late sweep to catch any asynchronously spawned props or story actors
            yield return new WaitForSeconds(1.5f);
            RunSolidificationPass();
        }

        private void RunSolidificationPass()
        {
            int addedColliders = 0;
            int addedDynamicRbs = 0;
            int upgradedActors = 0;

            // 1. Upgrade all saved-scene movable street props & static obstacles lacking colliders
            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform tr = allTransforms[i];
                if (tr == null) continue;
                GameObject go = tr.gameObject;
                string n = go.name;

                // A. Movable street props -> DynamicWorldProp with realistic dynamic Rigidbody!
                if (IsMovableSceneProp(n, go, out float massKg, out DynamicPropMaterialKind matKind))
                {
                    if (go.GetComponent<DynamicWorldProp>() == null && go.GetComponent<CarryableProp>() == null)
                    {
                        if (SetupDynamicProp(go, massKg, matKind))
                        {
                            addedDynamicRbs++;
                        }
                    }
                    continue;
                }

                // B. Static saved-scene obstacles missing colliders (31 street signs, pool loungers, umbrellas, notice boards, tyres, buoys)
                if (n.StartsWith("StreetSign_", StringComparison.OrdinalIgnoreCase))
                {
                    if (go.GetComponentInChildren<Collider>(true) == null)
                    {
                        CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
                        cc.center = new Vector3(0f, 1.25f, 0f);
                        cc.radius = 0.09f;
                        cc.height = 2.5f;
                        addedColliders++;
                    }
                }
                else if (n.StartsWith("Pool_SunLounger", StringComparison.OrdinalIgnoreCase) ||
                         n.StartsWith("NoticeBoardStand", StringComparison.OrdinalIgnoreCase) ||
                         n.StartsWith("Prop_OceanBuoy", StringComparison.OrdinalIgnoreCase))
                {
                    if (go.GetComponentInChildren<Collider>(true) == null)
                    {
                        if (EnsureSolidBoundsCollider(go, minSize: 0.25f))
                        {
                            addedColliders++;
                        }
                    }
                }
                else if (n.StartsWith("Pool_Umbrella", StringComparison.OrdinalIgnoreCase))
                {
                    if (go.GetComponentInChildren<Collider>(true) == null)
                    {
                        CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
                        cc.center = new Vector3(0f, 1.15f, 0f);
                        cc.radius = 0.12f;
                        cc.height = 2.3f;
                        addedColliders++;
                    }
                }
            }

            // 2. Ensure ALL moving/story NPCs & animals have kinematic Rigidbody + solid CapsuleCollider
            TownPedestrian[] peds = FindObjectsByType<TownPedestrian>(FindObjectsInactive.Exclude);
            for (int i = 0; i < peds.Length; i++)
            {
                if (peds[i] == null) continue;
                EnsureKinematicActorPhysics(peds[i].gameObject, radius: 0.32f, height: 1.78f, centerY: 0.89f);
                upgradedActors++;
            }

            SmartStoryNpcPresence[] storyNpcs = FindObjectsByType<SmartStoryNpcPresence>(FindObjectsInactive.Exclude);
            for (int i = 0; i < storyNpcs.Length; i++)
            {
                if (storyNpcs[i] == null) continue;
                EnsureKinematicActorPhysics(storyNpcs[i].gameObject, radius: 0.34f, height: 1.78f, centerY: 0.89f);
                upgradedActors++;
            }

            StreetCatController[] cats = FindObjectsByType<StreetCatController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < cats.Length; i++)
            {
                if (cats[i] == null) continue;
                EnsureKinematicActorPhysics(cats[i].gameObject, radius: 0.17f, height: 0.38f, centerY: 0.19f);
                upgradedActors++;
            }

            FarmAnimalController[] farmAnimals = FindObjectsByType<FarmAnimalController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < farmAnimals.Length; i++)
            {
                if (farmAnimals[i] == null) continue;
                FarmAnimalKind k = farmAnimals[i].Kind;
                float r = k == FarmAnimalKind.Cow ? 0.56f : (k == FarmAnimalKind.Sheep ? 0.38f : 0.20f);
                float h = k == FarmAnimalKind.Cow ? 1.45f : (k == FarmAnimalKind.Sheep ? 0.92f : 0.44f);
                EnsureKinematicActorPhysics(farmAnimals[i].gameObject, radius: r, height: h, centerY: h * 0.5f);
                upgradedActors++;
            }

            TailTargetVehicleController[] tailCars = FindObjectsByType<TailTargetVehicleController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < tailCars.Length; i++)
            {
                if (tailCars[i] == null) continue;
                EnsureKinematicRigidbody(tailCars[i].gameObject);
                if (tailCars[i].GetComponentInChildren<Collider>(true) == null)
                {
                    BoxCollider bc = tailCars[i].gameObject.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 0.72f, 0f);
                    bc.size = new Vector3(1.88f, 1.38f, 4.42f);
                }
                upgradedActors++;
            }

            if (addedColliders > 0 || addedDynamicRbs > 0)
            {
                Debug.Log($"[WorldPhysicsSolidifier] Solidified {addedColliders} static obstacles, upgraded {addedDynamicRbs} dynamic street props, verified {upgradedActors} kinematic actors.");
            }
        }

        private static bool IsMovableSceneProp(string name, GameObject go, out float massKg, out DynamicPropMaterialKind matKind)
        {
            massKg = 15f;
            matKind = DynamicPropMaterialKind.Wood;

            // Never convert static-batched children or vehicles/buildings/UI
            if (go.isStatic) return false;
            if (go.GetComponentInParent<DynamicWorldProp>() != null) return false;
            if (go.GetComponentInParent<CarryableProp>() != null) return false;
            if (go.GetComponentInParent<SedanVehicleController>() != null) return false;
            if (go.GetComponentInParent<SeatInteractable>() != null) return false;

            if (name.StartsWith("Prop_TrashCan", StringComparison.OrdinalIgnoreCase))
            {
                massKg = 18f;
                matKind = DynamicPropMaterialKind.Metal;
                return true;
            }
            if (name.StartsWith("Prop_WoodenCrate", StringComparison.OrdinalIgnoreCase))
            {
                massKg = 14f;
                matKind = DynamicPropMaterialKind.Wood;
                return true;
            }
            if (name.StartsWith("Prop_OilBarrel", StringComparison.OrdinalIgnoreCase))
            {
                massKg = 44f;
                matKind = DynamicPropMaterialKind.Metal;
                return true;
            }
            if (name.StartsWith("Prop_ToolCart", StringComparison.OrdinalIgnoreCase))
            {
                massKg = 34f;
                matKind = DynamicPropMaterialKind.Metal;
                return true;
            }
            if (name.StartsWith("Prop_OldTyre", StringComparison.OrdinalIgnoreCase))
            {
                massKg = 11f;
                matKind = DynamicPropMaterialKind.Rubber;
                return true;
            }
            return false;
        }

        private static bool SetupDynamicProp(GameObject go, float massKg, DynamicPropMaterialKind matKind)
        {
            // Ensure primitive collider (NEVER non-convex MeshCollider on a dynamic Rigidbody!)
            MeshCollider[] meshCols = go.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < meshCols.Length; i++)
            {
                if (meshCols[i] != null && !meshCols[i].convex)
                {
                    meshCols[i].convex = true;
                }
            }

            Collider existing = go.GetComponent<Collider>();
            if (existing == null)
            {
                if (!EnsureSolidBoundsCollider(go, minSize: 0.24f)) return false;
                existing = go.GetComponent<Collider>();
            }
            if (existing == null) return false;

            // Overlap check at spawn (Unity 3D PhysX Step 11): prevent frame-1 depenetration pop!
            Bounds b = existing.bounds;
            Vector3 halfExt = Vector3.Max(b.extents * 0.88f, Vector3.one * 0.08f);
            int overlaps = Physics.OverlapBoxNonAlloc(
                b.center + Vector3.up * 0.03f,
                halfExt,
                OverlapBuf,
                Quaternion.identity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            bool embeddedInStaticWall = false;
            for (int i = 0; i < overlaps; i++)
            {
                Collider other = OverlapBuf[i];
                if (other == null || other.transform.IsChildOf(go.transform) || go.transform.IsChildOf(other.transform)) continue;
                // Ignore flat ground directly underneath
                if (other.bounds.max.y <= b.min.y + 0.09f) continue;
                embeddedInStaticWall = true;
                break;
            }

            // Lift 1cm if bottom slightly clips ground plane
            if (b.min.y < 0.005f && b.min.y > -0.15f)
            {
                go.transform.position += Vector3.up * (0.012f - b.min.y);
            }

            DynamicWorldProp prop = go.AddComponent<DynamicWorldProp>();
            prop.Configure(massKg, matKind, startKinematicUntilHit: embeddedInStaticWall);
            return true;
        }

        // =====================================================================
        // SHARED STATIC PHYSICS HELPERS FOR ALL WORLD BUILDERS
        // =====================================================================

        public static Rigidbody EnsureKinematicRigidbody(GameObject go)
        {
            if (go == null) return null;
            // Never pair Rigidbody with CharacterController on the same object
            if (go.GetComponent<CharacterController>() != null) return null;

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            // Kinematic actors driven in Update() stay frame-exact with Interpolation.None (prevents 50Hz/120Hz double-step stutter)
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return rb;
        }

        public static void EnsureKinematicActorPhysics(GameObject go, float radius, float height, float centerY)
        {
            if (go == null) return;
            if (go.GetComponent<CharacterController>() != null) return;

            CapsuleCollider cap = go.GetComponent<CapsuleCollider>();
            if (cap == null)
            {
                cap = go.AddComponent<CapsuleCollider>();
                cap.radius = radius;
                cap.height = height;
                cap.center = new Vector3(0f, centerY, 0f);
                cap.isTrigger = false;
            }

            EnsureKinematicRigidbody(go);
        }

        public static void EnsureTrunkCollider(GameObject treeGo, float radius = 0.42f, float height = 3.2f)
        {
            if (treeGo == null || treeGo.GetComponent<Collider>() != null) return;
            CapsuleCollider cc = treeGo.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, height * 0.5f, 0f);
            cc.radius = radius;
            cc.height = height;
        }

        public static void EnsureBoxCollider(GameObject go, Vector3 center, Vector3 size)
        {
            if (go == null) return;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = center;
            bc.size = size;
        }

        public static bool EnsureSolidBoundsCollider(GameObject go, float minSize = 0.22f, float shrinkXZ = 0.94f)
        {
            if (go == null) return false;
            Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return false;

            bool hasAny = false;
            Bounds worldBounds = default;
            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!hasAny)
                {
                    worldBounds = r.bounds;
                    hasAny = true;
                }
                else
                {
                    worldBounds.Encapsulate(r.bounds);
                }
            }
            if (!hasAny) return false;

            Transform t = go.transform;
            Vector3 localCenter = t.InverseTransformPoint(worldBounds.center);
            Vector3 lossy = t.lossyScale;
            float sx = Mathf.Max(0.01f, Mathf.Abs(lossy.x));
            float sy = Mathf.Max(0.01f, Mathf.Abs(lossy.y));
            float sz = Mathf.Max(0.01f, Mathf.Abs(lossy.z));

            Vector3 localSize = new Vector3(
                Mathf.Max(minSize, (worldBounds.size.x * shrinkXZ) / sx),
                Mathf.Max(minSize, worldBounds.size.y / sy),
                Mathf.Max(minSize, (worldBounds.size.z * shrinkXZ) / sz));

            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = localCenter;
            bc.size = localSize;
            return true;
        }
    }
}
