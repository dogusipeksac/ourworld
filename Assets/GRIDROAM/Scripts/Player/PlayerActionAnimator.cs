using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace OurWorld.Player
{
    public enum PlayerActionKind
    {
        WashFace = 0,
        Abdest = 1,
        TypeComputer = 2,
        AtmKeypad = 3,
        UseTvRemote = 4,
        ChangeOutfit = 5,
        LoadWashingMachine = 6,
        RefuelCar = 7,
        InspectReach = 8,
        WakeStretch = 9,
        FarmPlantAndDig = 10,
        FarmWatering = 11,
        FarmHarvest = 12,
        MilkCow = 13,
        ShearSheep = 14,
        GrillMangal = 15,
        CollectEgg = 16,
        FeedAndPetAnimal = 17
    }

    /// <summary>
    /// Full-body procedural 2-bone IK + posture + handheld prop animator for every home, farm, and street
    /// interaction (washing hands/face at the sink, şadırvan abdest, typing on PC, entering ATM PIN,
    /// aiming TV remote, changing outfits at wardrobe, loading washing machine, fueling car,
    /// milking cows with stool/pail/milk streams, shearing sheep with articulated shears/wool piles,
    /// collecting eggs into a woven wicker basket, feeding/petting livestock, planting/watering/harvesting).
    /// </summary>
    [DefaultExecutionOrder(250)]
    public partial class PlayerActionAnimator : MonoBehaviour
    {
        private static PlayerActionAnimator _activeInstance;
        private static readonly Vector2[] AtmPinOffsets =
        {
            new Vector2(-0.05f,  0.04f),
            new Vector2( 0.04f,  0.04f),
            new Vector2(-0.02f, -0.02f),
            new Vector2( 0.05f, -0.05f)
        };

        private PlayerFootController _foot;
        private CharacterController _cc;
        private Coroutine _activeRoutine;
        private GameObject _heldProp;
        private GameObject _heldLeftProp;
        private GameObject _groundProp;
        private readonly List<GameObject> _extraProps = new List<GameObject>(8);
        private Quaternion _heldPropLocalRot = Quaternion.identity;
        private Quaternion _heldLeftPropLocalRot = Quaternion.identity;
        private bool _holdingModalLock;
        // Gameplay callbacks of the running action. Kept so an external cancel (another action/consume
        // starting, explicit CancelActive) still applies the outcome exactly once instead of dropping it.
        private Action _pendingClimax;
        private Action _pendingFinished;
        // Serial of the latest started run and of the latest run that reached its end. Lets StartAction detect a
        // routine that completed synchronously inside StartCoroutine (e.g. Esc pressed on the start frame while
        // already standing at the work spot) so IsPlaying never sticks at true and soft-locks IsBusyOn checks.
        private int _runSerial;
        private int _completedRunSerial;

        public bool IsPlaying => _activeRoutine != null;
        internal static bool IsAnyPlaying => _activeInstance != null && _activeInstance.IsPlaying;

        public static bool IsBusyOn(PlayerModeController mode)
        {
            if (mode == null || mode.FootController == null) return false;
            PlayerActionAnimator anim = mode.FootController.GetComponent<PlayerActionAnimator>();
            return anim != null && anim.IsPlaying;
        }

        public static void Play(
            PlayerModeController mode,
            Transform target,
            PlayerActionKind kind,
            Action onClimax = null,
            Action onFinished = null,
            string startStatus = null,
            Action<float> onProgress = null)
        {
            if (mode == null || mode.FootController == null || mode.CurrentMode != ControlMode.OnFoot)
            {
                onProgress?.Invoke(1f);
                onClimax?.Invoke();
                onFinished?.Invoke();
                return;
            }

            PlayerFootController foot = mode.FootController;
            PlayerConsumeAnimator consume = foot.GetComponent<PlayerConsumeAnimator>();
            if (consume != null && consume.IsPlaying)
            {
                consume.CancelActive();
            }

            PlayerActionAnimator anim = foot.GetComponent<PlayerActionAnimator>();
            if (anim == null)
            {
                anim = foot.gameObject.AddComponent<PlayerActionAnimator>();
            }
            anim.StartAction(mode, foot, target, kind, onClimax, onFinished, startStatus, onProgress);
        }

        private void Awake()
        {
            _foot = GetComponent<PlayerFootController>();
            _cc = GetComponent<CharacterController>();
            _activeInstance = this;
        }

        private void OnDisable()
        {
            // Teardown path (object disabled / scene unloading): release pose & locks, but don't run
            // gameplay callbacks that may touch objects already being destroyed.
            CancelActiveInternal(completeGameplay: false);
        }

        private void OnDestroy()
        {
            CancelActiveInternal(completeGameplay: false);
            if (_activeInstance == this) _activeInstance = null;
        }

        /// <summary>
        /// Stops the running action, restores pose/locks and applies its pending gameplay outcome
        /// (climax + finished) exactly once so interrupted interactions never leave half-applied state.
        /// </summary>
        public void CancelActive()
        {
            CancelActiveInternal(completeGameplay: true);
        }

        private void CancelActiveInternal(bool completeGameplay)
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
            CleanupVisualsAndLocks();

            Action climax = _pendingClimax;
            Action finished = _pendingFinished;
            _pendingClimax = null;
            _pendingFinished = null;
            if (!completeGameplay) return;
            climax?.Invoke();
            finished?.Invoke();
        }

        private void StartAction(
            PlayerModeController mode,
            PlayerFootController foot,
            Transform target,
            PlayerActionKind kind,
            Action onClimax,
            Action onFinished,
            string startStatus,
            Action<float> onProgress)
        {
            _foot = foot;
            if (_cc == null) _cc = foot.GetComponent<CharacterController>();
            CancelActive();

            bool climaxFired = false;
            Action climaxOnce = () =>
            {
                if (climaxFired) return;
                climaxFired = true;
                onClimax?.Invoke();
            };
            _pendingClimax = climaxOnce;
            _pendingFinished = onFinished;
            int serial = ++_runSerial;
            Coroutine routine = StartCoroutine(ActionRoutine(serial, mode, target, kind, climaxOnce, onFinished, startStatus, onProgress));
            if (_completedRunSerial != serial && _runSerial == serial)
            {
                _activeRoutine = routine;
            }
        }

        private IEnumerator ActionRoutine(
            int serial,
            PlayerModeController mode,
            Transform target,
            PlayerActionKind kind,
            Action onClimax,
            Action onFinished,
            string startStatus,
            Action<float> onProgress)
        {
            ProceduralHumanoid hum = _foot != null ? _foot.Humanoid : null;
            if (hum == null || mode == null || mode.CurrentMode != ControlMode.OnFoot)
            {
                _completedRunSerial = serial;
                _activeRoutine = null;
                _pendingClimax = null;
                _pendingFinished = null;
                onProgress?.Invoke(1f);
                onClimax?.Invoke();
                onFinished?.Invoke();
                yield break;
            }

            bool lockMovement = kind != PlayerActionKind.WakeStretch;
            if (lockMovement)
            {
                mode.SetModalLock(this, true);
                _holdingModalLock = true;
            }

            if (!string.IsNullOrEmpty(startStatus))
            {
                mode.RaiseStatusMessage(startStatus);
            }

            // Step 1: Smoothly approach the exact anatomical working spot (animal flank for MilkCow/ShearSheep,
            // exact ground egg for CollectEgg, or front stand distance for home/farm props!)
            if (target != null && kind != PlayerActionKind.WakeStretch)
            {
                bool flankWork = kind == PlayerActionKind.MilkCow || kind == PlayerActionKind.ShearSheep;
                bool homeRoutine = IsHomeRoutine(kind);
                Vector3 focusPoint = target.position;
                Vector3 standPos;
                float idealDist = GetIdealStandDistance(kind);

                if (flankWork)
                {
                    Vector3 flatRight = new Vector3(target.right.x, 0f, target.right.z).normalized;
                    if (flatRight.sqrMagnitude < 0.01f) flatRight = Vector3.right;
                    Vector3 flatFwd = new Vector3(target.forward.x, 0f, target.forward.z).normalized;
                    if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;

                    float sideSign = Vector3.Dot(_foot.transform.position - target.position, flatRight) >= 0f ? 1f : -1f;
                    float backShift = kind == PlayerActionKind.MilkCow ? -0.18f : -0.04f;
                    focusPoint = target.position + flatFwd * backShift;
                    standPos = new Vector3(focusPoint.x, _foot.transform.position.y, focusPoint.z) + flatRight * (sideSign * idealDist);
                }
                else if (homeRoutine && kind != PlayerActionKind.UseTvRemote && kind != PlayerActionKind.InspectReach)
                {
                    Vector3 surfacePt = GetTargetFrontSurfacePoint(target, _foot.transform.position + Vector3.up * 0.95f, out bool hasSurface);
                    Vector3 refPoint = hasSurface ? surfacePt : target.position;
                    Vector3 toRef = refPoint - _foot.transform.position;
                    toRef.y = 0f;
                    float dist = toRef.magnitude;
                    Vector3 approachDir = dist > 0.02f ? (toRef / dist) : _foot.transform.forward;
                    float clearance = hasSurface ? GetHomeSurfaceClearance(kind) : idealDist;
                    focusPoint = refPoint;
                    standPos = new Vector3(refPoint.x, _foot.transform.position.y, refPoint.z) - approachDir * clearance;
                }
                else
                {
                    Vector3 toTarget = target.position - _foot.transform.position;
                    toTarget.y = 0f;
                    float dist = toTarget.magnitude;
                    Vector3 approachDir = dist > 0.01f ? (toTarget / dist) : _foot.transform.forward;
                    standPos = new Vector3(target.position.x, _foot.transform.position.y, target.position.z) - approachDir * idealDist;
                }

                Vector3 deltaToStand = standPos - _foot.transform.position;
                deltaToStand.y = 0f;
                float standDist = deltaToStand.magnitude;
                float walkThreshold = flankWork ? 0.22f
                    : (kind == PlayerActionKind.CollectEgg ? 0.25f
                    : (homeRoutine ? 0.16f : 0.55f));

                if (standDist > walkThreshold && standDist < 4.2f)
                {
                    Vector3 toFocusInitial = focusPoint - _foot.transform.position;
                    toFocusInitial.y = 0f;
                    bool backstepping = homeRoutine && toFocusInitial.sqrMagnitude > 0.01f &&
                                        Vector3.Dot(deltaToStand, toFocusInitial) < 0f && standDist < 0.85f;
                    float maxWalk = flankWork ? 1.05f : (homeRoutine ? 1.25f : 0.72f);
                    float walkT = 0f;
                    float stuckTimer = 0f;
                    while (walkT < maxWalk)
                    {
                        if (ShouldAbort(mode)) break;
                        Vector3 d = standPos - _foot.transform.position;
                        d.y = 0f;
                        float rem = d.magnitude;
                        if (rem <= 0.12f) break;

                        float dt = Time.deltaTime;
                        walkT += dt;
                        Vector3 dir = d / Mathf.Max(0.001f, rem);
                        Vector3 faceVec = backstepping
                            ? Vector3.ProjectOnPlane(focusPoint - _foot.transform.position, Vector3.up)
                            : dir;
                        if (faceVec.sqrMagnitude > 0.001f)
                        {
                            _foot.transform.rotation = Quaternion.Slerp(
                                _foot.transform.rotation,
                                Quaternion.LookRotation(faceVec.normalized, Vector3.up),
                                1f - Mathf.Exp(-12f * dt));
                        }
                        float maxSpd = backstepping ? 1.45f : 2.4f;
                        float spd = Mathf.Min(maxSpd, rem * 4.5f);
                        Vector3 prevPos = _foot.transform.position;
                        if (_cc != null && _cc.enabled)
                        {
                            _cc.Move((dir * spd + Vector3.down * 4f) * dt);
                        }
                        Vector3 moved = Vector3.ProjectOnPlane(_foot.transform.position - prevPos, Vector3.up);
                        float actualSpd = dt > 0.0001f ? moved.magnitude / dt : spd;
                        hum.SetLocomotion(actualSpd, true, 0f, dir);

                        if (homeRoutine)
                        {
                            if (actualSpd < 0.16f)
                            {
                                stuckTimer += dt;
                                if (stuckTimer >= 0.18f) break;
                            }
                            else
                            {
                                stuckTimer = 0f;
                            }
                        }
                        yield return null;
                    }
                }

                hum.SetLocomotion(0f, true, 0f, _foot.transform.forward);
                Vector3 lookDelta = focusPoint - _foot.transform.position;
                lookDelta.y = 0f;
                if (lookDelta.sqrMagnitude > 0.004f)
                {
                    Quaternion faceRot = Quaternion.LookRotation(lookDelta.normalized, Vector3.up);
                    float maxTurn = homeRoutine ? 0.34f : 0.14f;
                    float turnT = 0f;
                    while (turnT < maxTurn)
                    {
                        if (ShouldAbort(mode)) break;
                        turnT += Time.deltaTime;
                        _foot.transform.rotation = Quaternion.Slerp(_foot.transform.rotation, faceRot, 1f - Mathf.Exp(-15f * Time.deltaTime));
                        hum.SetLocomotion(0f, true, 0f, _foot.transform.forward);
                        if (homeRoutine && Quaternion.Angle(_foot.transform.rotation, faceRot) <= 4f) break;
                        yield return null;
                    }
                }
            }

            bool climaxDone = false;
            Action triggerClimax = () =>
            {
                if (climaxDone) return;
                climaxDone = true;
                onClimax?.Invoke();
            };

            // Step 2: Execute bespoke physical 2-bone IK choreography for the specific interaction
            switch (kind)
            {
                case PlayerActionKind.WashFace:
                    yield return RunWashFace(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.Abdest:
                    yield return RunAbdest(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.TypeComputer:
                    yield return RunTypeComputer(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.AtmKeypad:
                    yield return RunAtmKeypad(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.UseTvRemote:
                    yield return RunUseTvRemote(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.ChangeOutfit:
                    yield return RunChangeOutfit(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.LoadWashingMachine:
                    yield return RunLoadWashingMachine(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.RefuelCar:
                    yield return RunRefuelCar(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.InspectReach:
                    yield return RunInspectReach(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.WakeStretch:
                    yield return RunWakeStretch(mode, hum, triggerClimax);
                    break;
                case PlayerActionKind.FarmPlantAndDig:
                    yield return RunFarmPlantAndDig(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.FarmWatering:
                    yield return RunFarmWatering(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.FarmHarvest:
                    yield return RunFarmHarvest(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.MilkCow:
                    yield return RunMilkCow(mode, hum, target, triggerClimax, onProgress);
                    break;
                case PlayerActionKind.ShearSheep:
                    yield return RunShearSheep(mode, hum, target, triggerClimax, onProgress);
                    break;
                case PlayerActionKind.GrillMangal:
                    yield return RunGrillMangal(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.CollectEgg:
                    yield return RunCollectEgg(mode, hum, target, triggerClimax, onProgress);
                    break;
                case PlayerActionKind.FeedAndPetAnimal:
                    yield return RunFeedAndPetAnimal(mode, hum, target, triggerClimax, onProgress);
                    break;
            }

            // Guarantee gameplay outcome always completes even if animation ended early
            _completedRunSerial = serial;
            _pendingClimax = null;
            _pendingFinished = null;
            triggerClimax();
            CleanupVisualsAndLocks();
            _activeRoutine = null;
            onFinished?.Invoke();
        }


        private static bool IsHomeRoutine(PlayerActionKind kind)
        {
            switch (kind)
            {
                case PlayerActionKind.WashFace:
                case PlayerActionKind.Abdest:
                case PlayerActionKind.TypeComputer:
                case PlayerActionKind.AtmKeypad:
                case PlayerActionKind.UseTvRemote:
                case PlayerActionKind.ChangeOutfit:
                case PlayerActionKind.LoadWashingMachine:
                case PlayerActionKind.RefuelCar:
                case PlayerActionKind.InspectReach:
                case PlayerActionKind.WakeStretch:
                case PlayerActionKind.GrillMangal:
                    return true;
                default:
                    return false;
            }
        }

        private static float GetHomeSurfaceClearance(PlayerActionKind kind)
        {
            switch (kind)
            {
                case PlayerActionKind.WashFace:
                case PlayerActionKind.TypeComputer:
                    return 0.42f;
                case PlayerActionKind.Abdest:
                case PlayerActionKind.AtmKeypad:
                    return 0.45f;
                case PlayerActionKind.RefuelCar:
                    return 0.46f;
                case PlayerActionKind.LoadWashingMachine:
                    return 0.48f;
                case PlayerActionKind.ChangeOutfit:
                case PlayerActionKind.GrillMangal:
                    return 0.50f;
                default:
                    return 0.46f;
            }
        }

        private static Vector3 GetTargetFrontSurfacePoint(Transform target, Vector3 referenceWorld, out bool hasSurface)
        {
            hasSurface = false;
            if (target == null) return referenceWorld;

            Collider[] cols = target.GetComponentsInChildren<Collider>(false);
            Vector3 bestPoint = target.position;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < cols.Length; i++)
            {
                Collider col = cols[i];
                if (col == null || !col.enabled || col.isTrigger) continue;
                bool supportsExact = col is BoxCollider || col is SphereCollider || col is CapsuleCollider ||
                                     (col is MeshCollider mc && mc.convex);
                Vector3 pt = supportsExact ? col.ClosestPoint(referenceWorld) : col.bounds.ClosestPoint(referenceWorld);
                Vector3 flatDelta = pt - referenceWorld;
                flatDelta.y = 0f;
                float dSq = flatDelta.sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestPoint = pt;
                    hasSurface = true;
                }
            }
            return bestPoint;
        }

        private Vector2 ResolveHomeForwardAndHeight(
            Transform target,
            float defaultFwd,
            float defaultY,
            float minFwd,
            float maxFwd,
            float minY,
            float maxY,
            float surfacePullback = 0f)
        {
            if (target == null || _foot == null) return new Vector2(defaultFwd, defaultY);
            Transform tr = _foot.transform;
            Vector3 probe = tr.position + Vector3.up * defaultY;
            Vector3 surfacePt = GetTargetFrontSurfacePoint(target, probe, out bool hasSurface);
            Vector3 refPt = hasSurface ? surfacePt : target.position;
            Vector3 flatDelta = Vector3.ProjectOnPlane(refPt - tr.position, Vector3.up);
            float fwdProj = Vector3.Dot(flatDelta, tr.forward) - surfacePullback;
            float resolvedFwd = fwdProj > 0.05f ? Mathf.Clamp(fwdProj, minFwd, maxFwd) : defaultFwd;

            float resolvedY = defaultY;
            float targetRelY = refPt.y - tr.position.y;
            if (targetRelY >= minY && targetRelY <= maxY)
            {
                resolvedY = targetRelY;
            }
            return new Vector2(resolvedFwd, resolvedY);
        }

        private static float GetIdealStandDistance(PlayerActionKind kind)
        {
            switch (kind)
            {
                case PlayerActionKind.WashFace:
                case PlayerActionKind.Abdest:
                    return 0.72f;
                case PlayerActionKind.TypeComputer:
                case PlayerActionKind.LoadWashingMachine:
                    return 0.78f;
                case PlayerActionKind.AtmKeypad:
                case PlayerActionKind.ChangeOutfit:
                    return 0.82f;
                case PlayerActionKind.RefuelCar:
                    return 1.35f;
                case PlayerActionKind.InspectReach:
                    return 0.64f;
                case PlayerActionKind.UseTvRemote:
                    return 1.55f;
                case PlayerActionKind.FarmPlantAndDig:
                case PlayerActionKind.FarmWatering:
                case PlayerActionKind.FarmHarvest:
                    return 1.10f;
                case PlayerActionKind.MilkCow:
                    return 1.12f;
                case PlayerActionKind.ShearSheep:
                    return 0.94f;
                case PlayerActionKind.CollectEgg:
                    return 0.66f;
                case PlayerActionKind.FeedAndPetAnimal:
                    return 1.32f;
                case PlayerActionKind.GrillMangal:
                    return 1.05f;
                default:
                    return 0.85f;
            }
        }

        private static float EdgeBlend(float norm, float fadeInEnd, float fadeOutStart)
        {
            float inB = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(norm / Mathf.Max(0.01f, fadeInEnd)));
            float outB = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - fadeOutStart) / Mathf.Max(0.01f, 1f - fadeOutStart)));
            return inB * outB;
        }

        private bool ShouldAbort(PlayerModeController mode)
        {
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot) return true;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
            return false;
        }

        private void CleanupVisualsAndLocks()
        {
            if (_foot != null && _foot.Humanoid != null)
            {
                _foot.Humanoid.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
                _foot.Humanoid.SetKneel(0f);
                _foot.Humanoid.SetLookAtPoint(false, Vector3.zero);
            }

            if (_heldProp != null)
            {
                Destroy(_heldProp);
                _heldProp = null;
            }
            if (_heldLeftProp != null)
            {
                Destroy(_heldLeftProp);
                _heldLeftProp = null;
            }
            if (_groundProp != null)
            {
                Destroy(_groundProp);
                _groundProp = null;
            }
            for (int i = 0; i < _extraProps.Count; i++)
            {
                if (_extraProps[i] != null) Destroy(_extraProps[i]);
            }
            _extraProps.Clear();
            _heldPropLocalRot = Quaternion.identity;
            _heldLeftPropLocalRot = Quaternion.identity;

            if (_holdingModalLock && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
                _holdingModalLock = false;
            }
        }

        private void LateUpdate()
        {
            if (_foot == null || _foot.Humanoid == null) return;
            if (_heldProp != null && _foot.Humanoid.RightForearm != null)
            {
                Vector3 palmWorld = _foot.Humanoid.RightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
                _heldProp.transform.position = palmWorld + _foot.transform.forward * 0.03f;
                _heldProp.transform.rotation = _foot.transform.rotation * _heldPropLocalRot;
            }
            if (_heldLeftProp != null && _foot.Humanoid.LeftForearm != null)
            {
                Vector3 lPalmWorld = _foot.Humanoid.LeftForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
                _heldLeftProp.transform.position = lPalmWorld + _foot.transform.forward * 0.03f;
                _heldLeftProp.transform.rotation = _foot.transform.rotation * _heldLeftPropLocalRot;
            }
        }

    }
}
