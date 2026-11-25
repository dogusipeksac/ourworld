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
    public class PlayerActionAnimator : MonoBehaviour
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
            CancelActive();
        }

        private void OnDestroy()
        {
            CancelActive();
            if (_activeInstance == this) _activeInstance = null;
        }

        public void CancelActive()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
            CleanupVisualsAndLocks();
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
            _activeRoutine = StartCoroutine(ActionRoutine(mode, target, kind, onClimax, onFinished, startStatus, onProgress));
        }

        private IEnumerator ActionRoutine(
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
                onProgress?.Invoke(1f);
                onClimax?.Invoke();
                onFinished?.Invoke();
                _activeRoutine = null;
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
            triggerClimax();
            CleanupVisualsAndLocks();
            _activeRoutine = null;
            onFinished?.Invoke();
        }

        private IEnumerator RunWashFace(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 2.75f;
            float t = 0f;
            bool splashHands = false;
            bool splashFace1 = false;
            bool splashFace2 = false;
            Color waterCol = new Color(0.46f, 0.88f, 1.0f);
            Vector2 basinLocal = ResolveHomeForwardAndHeight(target, 0.44f, 0.94f, 0.36f, 0.54f, 0.86f, 1.02f, surfacePullback: -0.04f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.10f, 0.88f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;
                Vector3 basinCenter = basePos + fwd * basinLocal.x + Vector3.up * basinLocal.y;
                Vector3 faceCenter = hum.HeadTransform != null
                    ? hum.HeadTransform.position + fwd * 0.14f - Vector3.up * 0.02f
                    : basePos + fwd * 0.24f + Vector3.up * 1.46f;

                Vector3 leftTarget;
                Vector3 rightTarget;
                float spineLean;
                float headPitch;

                if (t < 0.95f)
                {
                    // Phase 1: Rubbing both hands together under the faucet (fade rub oscillation cleanly before scoop phase)
                    float rub = t * 14f;
                    float rubEnv = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.80f) / 0.15f));
                    float baseSpread = Mathf.Lerp(0.08f, 0.055f, rubEnv);
                    leftTarget = basinCenter - right * (baseSpread + Mathf.Sin(rub) * 0.035f * rubEnv) + fwd * (Mathf.Cos(rub) * 0.035f * rubEnv) + Vector3.up * (Mathf.Sin(rub * 0.5f) * 0.02f * rubEnv);
                    rightTarget = basinCenter + right * (baseSpread - Mathf.Sin(rub) * 0.035f * rubEnv) - fwd * (Mathf.Cos(rub) * 0.035f * rubEnv) - Vector3.up * (Mathf.Sin(rub * 0.5f) * 0.02f * rubEnv);
                    spineLean = Mathf.Lerp(22f, 24f, rubEnv) * blend;
                    headPitch = Mathf.Lerp(18f, 22f, rubEnv) * blend;
                    hum.SetLookAtPoint(true, basinCenter);

                    if (!splashHands && t >= 0.22f)
                    {
                        splashHands = true;
                        GameFeelAudio.PlayWaterSplash();
                        GameFeelAudio.SpawnBurst(basinCenter, waterCol, count: 8, spread: 0.28f);
                    }
                }
                else
                {
                    // Phase 2: Two full double-handed water scoops to the face + cheek rub
                    float scoop1 = Mathf.Sin(Mathf.Clamp01((t - 0.95f) / 0.72f) * Mathf.PI);
                    float scoop2 = Mathf.Sin(Mathf.Clamp01((t - 1.70f) / 0.72f) * Mathf.PI);
                    float lift = Mathf.Max(scoop1, scoop2);
                    float rubCheek = Mathf.Sin(t * 20f) * 0.025f * lift;

                    Vector3 leftBasin = basinCenter - right * 0.08f;
                    Vector3 rightBasin = basinCenter + right * 0.08f;
                    Vector3 leftCheek = faceCenter - right * 0.085f + Vector3.up * rubCheek;
                    Vector3 rightCheek = faceCenter + right * 0.085f - Vector3.up * rubCheek;

                    leftTarget = Vector3.Lerp(leftBasin, leftCheek, lift);
                    rightTarget = Vector3.Lerp(rightBasin, rightCheek, lift);
                    spineLean = Mathf.Lerp(22f, 27f, lift) * blend;
                    headPitch = Mathf.Lerp(18f, 32f, lift) * blend;
                    hum.SetLookAtPoint(true, Vector3.Lerp(basinCenter, basinCenter + Vector3.up * 0.45f, lift));

                    if (!splashFace1 && t >= 1.30f)
                    {
                        splashFace1 = true;
                        GameFeelAudio.PlayWaterSplash();
                        GameFeelAudio.SpawnBurst(faceCenter, waterCol, count: 9, spread: 0.30f);
                    }
                    if (!splashFace2 && t >= 2.05f)
                    {
                        splashFace2 = true;
                        GameFeelAudio.PlayWaterSplash();
                        GameFeelAudio.SpawnBurst(faceCenter, waterCol, count: 10, spread: 0.34f);
                        triggerClimax();
                    }
                }

                hum.SetDualHandAction(true, leftTarget, rightTarget, blend, blend, spineLean, headPitch, 0.03f * blend);
                yield return null;
            }
        }

        private IEnumerator RunAbdest(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 3.45f;
            float t = 0f;
            bool splash1 = false;
            bool splash2 = false;
            bool splash3 = false;
            Color waterCol = new Color(0.42f, 0.86f, 1.0f);
            Vector2 tapLocal = ResolveHomeForwardAndHeight(target, 0.46f, 0.92f, 0.36f, 0.56f, 0.84f, 1.02f, surfacePullback: -0.02f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.08f, 0.90f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;
                Vector3 tapPos = basePos + fwd * tapLocal.x + Vector3.up * tapLocal.y;
                Vector3 faceCenter = hum.HeadTransform != null
                    ? hum.HeadTransform.position + fwd * 0.14f - Vector3.up * 0.02f
                    : basePos + fwd * 0.24f + Vector3.up * 1.46f;
                hum.SetLookAtPoint(true, tapPos);

                Vector3 leftTarget;
                Vector3 rightTarget;
                float spineLean = 22f * blend;
                float headPitch = 22f * blend;

                if (t < 0.95f)
                {
                    // 1. Wash both hands (fade rub cleanly into scoop start)
                    float rub = t * 14f;
                    float rubEnv = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.80f) / 0.15f));
                    float baseSpread = Mathf.Lerp(0.08f, 0.055f, rubEnv);
                    leftTarget = tapPos - right * (baseSpread + Mathf.Sin(rub) * 0.035f * rubEnv) + fwd * (Mathf.Cos(rub) * 0.03f * rubEnv);
                    rightTarget = tapPos + right * (baseSpread - Mathf.Sin(rub) * 0.035f * rubEnv) - fwd * (Mathf.Cos(rub) * 0.03f * rubEnv);
                    headPitch = Mathf.Lerp(18f, 22f, rubEnv) * blend;
                    if (!splash1 && t >= 0.20f)
                    {
                        splash1 = true;
                        GameFeelAudio.PlayWaterSplash();
                        GameFeelAudio.SpawnBurst(tapPos, waterCol, count: 8, spread: 0.30f);
                    }
                }
                else if (t < 2.05f)
                {
                    // 2. Wash face twice
                    float scoop1 = Mathf.Sin(Mathf.Clamp01((t - 0.95f) / 0.52f) * Mathf.PI);
                    float scoop2 = Mathf.Sin(Mathf.Clamp01((t - 1.50f) / 0.52f) * Mathf.PI);
                    float lift = Mathf.Max(scoop1, scoop2);
                    leftTarget = Vector3.Lerp(tapPos - right * 0.08f, faceCenter - right * 0.08f, lift);
                    rightTarget = Vector3.Lerp(tapPos + right * 0.08f, faceCenter + right * 0.08f, lift);
                    headPitch = Mathf.Lerp(18f, 30f, lift) * blend;

                    if (!splash2 && t >= 1.25f)
                    {
                        splash2 = true;
                        GameFeelAudio.PlayWaterSplash();
                        GameFeelAudio.SpawnBurst(faceCenter, waterCol, count: 9, spread: 0.32f);
                    }
                }
                else
                {
                    // 3. Wash right forearm then left forearm (smooth entry & side swap)
                    float entry = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 2.05f) / 0.14f));
                    float swap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 2.52f) / 0.16f));
                    float wipe = Mathf.Sin((t - 2.05f) * 11f) * 0.10f;

                    Vector3 rArmR = tapPos + right * 0.04f + Vector3.up * 0.05f;
                    Vector3 rArmL = rArmR - fwd * (0.06f + wipe) + Vector3.up * 0.03f;
                    Vector3 lArmL = tapPos - right * 0.04f + Vector3.up * 0.05f;
                    Vector3 lArmR = lArmL - fwd * (0.06f + wipe) + Vector3.up * 0.03f;

                    Vector3 washL = Vector3.Lerp(rArmL, lArmL, swap);
                    Vector3 washR = Vector3.Lerp(rArmR, lArmR, swap);
                    leftTarget = Vector3.Lerp(tapPos - right * 0.08f, washL, entry);
                    rightTarget = Vector3.Lerp(tapPos + right * 0.08f, washR, entry);
                    spineLean = Mathf.Lerp(22f, 18f, entry) * blend;
                    headPitch = Mathf.Lerp(18f, 20f, entry) * blend;

                    if (!splash3 && t >= 2.28f)
                    {
                        splash3 = true;
                        GameFeelAudio.PlayWaterSplash();
                        GameFeelAudio.SpawnBurst(tapPos + Vector3.up * 0.08f, waterCol, count: 10, spread: 0.36f);
                        triggerClimax();
                    }
                }

                hum.SetDualHandAction(true, leftTarget, rightTarget, blend, blend, spineLean, headPitch, 0.025f * blend);
                yield return null;
            }
        }

        private IEnumerator RunTypeComputer(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 2.15f;
            float t = 0f;
            float nextClick = 0.25f;
            Vector2 deskLocal = ResolveHomeForwardAndHeight(target, 0.42f, 0.96f, 0.34f, 0.52f, 0.90f, 1.04f, surfacePullback: -0.02f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.12f, 0.86f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 deskPos = tr.position + fwd * deskLocal.x + Vector3.up * deskLocal.y;
                Vector3 screenFocus = deskPos + fwd * 0.24f + Vector3.up * 0.34f + right * (Mathf.Sin(t * 2.4f) * 0.08f);
                hum.SetLookAtPoint(true, screenFocus);

                float phaseL = Mathf.Sin(t * 22f);
                float phaseR = Mathf.Sin(t * 22f + Mathf.PI);
                Vector3 leftTarget = deskPos - right * (0.13f + Mathf.Cos(t * 7f) * 0.03f) + Vector3.up * (Mathf.Max(0f, phaseL) * 0.036f);
                Vector3 rightTarget = deskPos + right * (0.13f + Mathf.Sin(t * 8f) * 0.03f) + Vector3.up * (Mathf.Max(0f, phaseR) * 0.036f);

                if (t >= nextClick && t < total - 0.35f)
                {
                    nextClick += 0.26f;
                    GameFeelAudio.PlayUiClick();
                }
                if (norm >= 0.80f)
                {
                    triggerClimax();
                }

                hum.SetDualHandAction(true, leftTarget, rightTarget, blend, blend, 11f * blend, 12f * blend, 0f);
                yield return null;
            }
        }

        private IEnumerator RunAtmKeypad(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.75f;
            float t = 0f;
            int pressedCount = 0;
            Vector2 padLocal = ResolveHomeForwardAndHeight(target, 0.45f, 1.24f, 0.36f, 0.54f, 1.16f, 1.32f, surfacePullback: 0.02f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.12f, 0.86f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 padCenter = tr.position + fwd * padLocal.x + right * 0.06f + Vector3.up * padLocal.y;
                Vector3 slotPos = tr.position + fwd * Mathf.Max(0.36f, padLocal.x - 0.03f) + right * 0.08f + Vector3.up * (padLocal.y - 0.16f);
                hum.SetLookAtPoint(true, padCenter + Vector3.up * 0.16f);

                Vector3 rightTarget;
                if (t < 1.15f)
                {
                    int digitIdx = Mathf.Clamp(Mathf.FloorToInt(t / 0.28f), 0, 3);
                    float slotT = Mathf.Clamp01((t - digitIdx * 0.28f) / 0.28f);
                    // Glide smoothly from previous key to next key before pressing forward (eliminates 1-frame snap)
                    Vector2 prevOff = digitIdx > 0 ? AtmPinOffsets[digitIdx - 1] : Vector2.zero;
                    Vector2 curOff = AtmPinOffsets[digitIdx];
                    float moveU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slotT / 0.34f));
                    Vector2 off = Vector2.Lerp(prevOff, curOff, moveU);
                    float localPress = slotT >= 0.25f
                        ? Mathf.Sin(Mathf.Clamp01((slotT - 0.25f) / 0.65f) * Mathf.PI)
                        : 0f;
                    rightTarget = padCenter + right * off.x + Vector3.up * off.y + fwd * (localPress * 0.055f);

                    if (digitIdx + 1 > pressedCount && localPress > 0.72f)
                    {
                        pressedCount = digitIdx + 1;
                        GameFeelAudio.PlayUiClick();
                    }
                }
                else
                {
                    // Reach continuously from last PIN key down to card/cash dispenser slot & pull back
                    Vector3 lastKeyPos = padCenter + right * AtmPinOffsets[3].x + Vector3.up * AtmPinOffsets[3].y;
                    Vector3 retractPos = tr.position + fwd * 0.26f + right * 0.12f + Vector3.up * 1.05f;
                    float phase2 = Mathf.Clamp01((t - 1.15f) / 0.50f);
                    if (phase2 < 0.45f)
                    {
                        rightTarget = Vector3.Lerp(lastKeyPos, slotPos, Mathf.SmoothStep(0f, 1f, phase2 / 0.45f));
                    }
                    else
                    {
                        rightTarget = Vector3.Lerp(slotPos, retractPos, Mathf.SmoothStep(0f, 1f, (phase2 - 0.45f) / 0.55f));
                    }
                    if (t >= 1.38f)
                    {
                        triggerClimax();
                    }
                }

                // Left hand rests naturally at waist level
                Vector3 leftRest = tr.position + fwd * 0.24f - right * 0.16f + Vector3.up * 1.02f;
                hum.SetDualHandAction(true, leftRest, rightTarget, blend * 0.45f, blend, 7f * blend, 8f * blend, 0f);
                yield return null;
            }
        }

        private IEnumerator RunUseTvRemote(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            _heldProp = SpawnActionProp(hum.RightForearm, PlayerActionKind.UseTvRemote);
            const float total = 1.15f;
            float t = 0f;
            bool clicked1 = false;
            bool clicked2 = false;

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.18f, 0.78f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 tvFocus = target != null
                    ? new Vector3(target.position.x, Mathf.Clamp(target.position.y + 0.95f, tr.position.y + 0.90f, tr.position.y + 1.85f), target.position.z)
                    : (tr.position + fwd * 1.8f + Vector3.up * 1.35f);
                hum.SetLookAtPoint(true, tvFocus);

                float jab1 = Mathf.Sin(Mathf.Clamp01((t - 0.28f) / 0.22f) * Mathf.PI);
                float jab2 = Mathf.Sin(Mathf.Clamp01((t - 0.58f) / 0.22f) * Mathf.PI);
                float jab = Mathf.Max(jab1, jab2);

                float aimY = Mathf.Clamp(1.22f + (tvFocus.y - (tr.position.y + 1.25f)) * 0.25f, 1.14f, 1.34f);
                Vector3 aimPos = tr.position + fwd * (0.48f + jab * 0.055f) + right * 0.12f + Vector3.up * (aimY + jab * 0.018f);
                _heldPropLocalRot = Quaternion.Euler(-4f + jab * 10f, 0f, 0f);

                if (!clicked1 && t >= 0.39f)
                {
                    clicked1 = true;
                    GameFeelAudio.PlayUiClick();
                    triggerClimax();
                }
                if (!clicked2 && t >= 0.69f)
                {
                    clicked2 = true;
                    GameFeelAudio.PlayUiClick();
                }

                hum.SetDualHandAction(true, Vector3.zero, aimPos, 0f, blend, 3f * blend, -3f * blend, 0f);
                yield return null;
            }
        }

        private IEnumerator RunChangeOutfit(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.85f;
            float t = 0f;
            bool latchPlayed = false;
            Vector2 doorLocal = ResolveHomeForwardAndHeight(target, 0.46f, 1.24f, 0.36f, 0.54f, 1.18f, 1.32f, surfacePullback: 0.02f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.12f, 0.86f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;

                Vector3 leftTarget;
                Vector3 rightTarget;
                float spineLean;
                float headPitch;

                if (t < 0.80f)
                {
                    // Stage 1: Reach forward and pull wardrobe doors wide open
                    float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.70f));
                    float spread = Mathf.Lerp(0.10f, 0.34f, open);
                    float depth = Mathf.Lerp(doorLocal.x, Mathf.Max(0.32f, doorLocal.x - 0.12f), open);
                    leftTarget = basePos + fwd * depth - right * spread + Vector3.up * doorLocal.y;
                    rightTarget = basePos + fwd * depth + right * spread + Vector3.up * doorLocal.y;
                    spineLean = 8f * blend;
                    headPitch = 4f * blend;
                    hum.SetLookAtPoint(true, basePos + fwd * doorLocal.x + Vector3.up * (doorLocal.y + 0.12f));

                    if (!latchPlayed && t >= 0.25f)
                    {
                        latchPlayed = true;
                        GameFeelAudio.PlayDoorLatch();
                    }
                }
                else
                {
                    // Stage 2: Smoothly transition from open wardrobe doors to jacket lapel brush
                    if (t >= 0.84f)
                    {
                        triggerClimax();
                    }
                    float trans = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.80f) / 0.18f));
                    float brush = Mathf.Clamp01((t - 0.80f) / 0.85f);
                    float yBrush = Mathf.Lerp(1.36f, 1.00f, brush);
                    float sway = Mathf.Sin(brush * Mathf.PI) * 0.04f;
                    float openDepth = Mathf.Max(0.32f, doorLocal.x - 0.12f);
                    Vector3 openL = basePos + fwd * openDepth - right * 0.34f + Vector3.up * doorLocal.y;
                    Vector3 openR = basePos + fwd * openDepth + right * 0.34f + Vector3.up * doorLocal.y;
                    Vector3 brushL = basePos + fwd * (0.22f + sway) - right * 0.13f + Vector3.up * yBrush;
                    Vector3 brushR = basePos + fwd * (0.22f + sway) + right * 0.13f + Vector3.up * yBrush;
                    leftTarget = Vector3.Lerp(openL, brushL, trans);
                    rightTarget = Vector3.Lerp(openR, brushR, trans);
                    spineLean = Mathf.Lerp(8f, -4f, trans) * blend;
                    headPitch = Mathf.Lerp(4f, 16f, trans) * blend;
                    hum.SetLookAtPoint(true, basePos + fwd * 0.24f + Vector3.up * 1.05f);
                }

                hum.SetDualHandAction(true, leftTarget, rightTarget, blend, blend, spineLean, headPitch, 0f);
                yield return null;
            }
        }

        private IEnumerator RunLoadWashingMachine(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.95f;
            float t = 0f;
            bool drumClosed = false;
            bool dialPressed = false;
            Vector2 washerLocal = ResolveHomeForwardAndHeight(target, 0.42f, 0.68f, 0.34f, 0.52f, 0.56f, 0.78f, surfacePullback: 0.02f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.12f, 0.86f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;

                Vector3 leftTarget;
                Vector3 rightTarget;
                float spineLean;
                float headPitch;
                float hipsDrop;

                if (t < 1.05f)
                {
                    // Stage 1: Squat down naturally (knees + hips, less back hunch) to load clothes & close drum
                    float push = Mathf.Sin(Mathf.Clamp01(t / 1.0f) * Mathf.PI);
                    leftTarget = basePos + fwd * (washerLocal.x + push * 0.07f) - right * 0.11f + Vector3.up * washerLocal.y;
                    rightTarget = basePos + fwd * (washerLocal.x + push * 0.07f) + right * 0.11f + Vector3.up * washerLocal.y;
                    spineLean = 26f * blend;
                    headPitch = 22f * blend;
                    hipsDrop = 0.18f * blend;
                    hum.SetLookAtPoint(true, basePos + fwd * washerLocal.x + Vector3.up * washerLocal.y);

                    if (!drumClosed && t >= 0.72f)
                    {
                        drumClosed = true;
                        GameFeelAudio.PlayDoorLatch();
                    }
                }
                else
                {
                    // Stage 2: Rise smoothly from drum and press the start program dial with right hand
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.05f) / 0.24f));
                    float press = Mathf.Sin(Mathf.Clamp01((t - 1.20f) / 0.48f) * Mathf.PI);
                    Vector3 drumL = basePos + fwd * washerLocal.x - right * 0.11f + Vector3.up * washerLocal.y;
                    Vector3 drumR = basePos + fwd * washerLocal.x + right * 0.11f + Vector3.up * washerLocal.y;
                    Vector3 dialL = basePos + fwd * 0.24f - right * 0.18f + Vector3.up * 0.94f;
                    Vector3 dialR = basePos + fwd * (washerLocal.x + 0.02f + press * 0.055f) + right * 0.09f + Vector3.up * (washerLocal.y + 0.34f);
                    leftTarget = Vector3.Lerp(drumL, dialL, rise);
                    rightTarget = Vector3.Lerp(drumR, dialR, rise);
                    spineLean = Mathf.Lerp(26f, 11f, rise) * blend;
                    headPitch = Mathf.Lerp(22f, 14f, rise) * blend;
                    hipsDrop = Mathf.Lerp(0.18f, 0.02f, rise) * blend;
                    hum.SetLookAtPoint(true, dialR);

                    if (!dialPressed && t >= 1.38f)
                    {
                        dialPressed = true;
                        GameFeelAudio.PlayUiClick();
                        triggerClimax();
                    }
                }

                hum.SetDualHandAction(true, leftTarget, rightTarget, blend, blend, spineLean, headPitch, hipsDrop);
                yield return null;
            }
        }

        private IEnumerator RunRefuelCar(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            _heldProp = SpawnActionProp(hum.RightForearm, PlayerActionKind.RefuelCar);
            const float total = 2.45f;
            float t = 0f;
            float nextSplash = 0.55f;
            Vector2 tankLocal = ResolveHomeForwardAndHeight(target, 0.48f, 1.00f, 0.36f, 0.56f, 0.90f, 1.12f, surfacePullback: 0.04f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.12f, 0.86f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;

                // Insert nozzle cleanly into fuel port (0..0.16), hold & pump (0.16..0.82), withdraw cleanly (0.82..1.0)
                float insertU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(norm / 0.16f)) *
                                (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - 0.82f) / 0.16f)));
                float pulse = Mathf.Sin(t * 7f) * 0.012f * insertU;
                float nozzleFwd = Mathf.Lerp(tankLocal.x - 0.14f, tankLocal.x, insertU);
                Vector3 rightNozzle = basePos + fwd * nozzleFwd + right * 0.10f + Vector3.up * (tankLocal.y + pulse);
                Vector3 leftSupport = basePos + fwd * 0.24f - right * 0.19f + Vector3.up * 0.98f;
                _heldPropLocalRot = Quaternion.Euler(14f * insertU, 0f, 0f);
                hum.SetLookAtPoint(true, rightNozzle + fwd * 0.10f);

                if (t >= nextSplash && t < total - 0.35f)
                {
                    nextSplash += 0.72f;
                    GameFeelAudio.PlayWaterSplash();
                }
                if (norm >= 0.85f)
                {
                    triggerClimax();
                }

                hum.SetDualHandAction(true, leftSupport, rightNozzle, blend * 0.55f, blend, 9f * blend, 12f * blend, 0f);
                yield return null;
            }
        }

        private IEnumerator RunInspectReach(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 0.85f;
            float t = 0f;
            bool fired = false;

            Transform tr0 = _foot.transform;
            Vector3 localTargetOffset = new Vector3(0.10f, 1.18f, 0.46f);
            if (target != null)
            {
                Vector3 rel = tr0.InverseTransformPoint(target.position);
                localTargetOffset = new Vector3(
                    Mathf.Clamp(rel.x, -0.30f, 0.34f),
                    Mathf.Clamp(rel.y, 0.52f, 1.66f),
                    Mathf.Clamp(rel.z, 0.28f, 0.62f));
            }
            float lowCrouch = Mathf.Clamp01((0.92f - localTargetOffset.y) / 0.42f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);

                Transform tr = _foot.transform;
                Vector3 basePos = tr.position;
                Vector3 restPos = basePos + tr.forward * 0.24f + tr.right * 0.20f + Vector3.up * 0.94f;
                Vector3 targetPos = tr.TransformPoint(localTargetOffset);
                Vector3 chestPos = basePos + tr.forward * 0.36f + tr.right * 0.08f + Vector3.up * 1.16f;

                Vector3 handPos;
                float weight;
                float crouchEnv;
                if (norm < 0.44f)
                {
                    // Smooth ease-in/out reach to the actual target height & depth
                    float u = Mathf.SmoothStep(0f, 1f, norm / 0.44f);
                    handPos = Vector3.Lerp(restPos, targetPos, u);
                    weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(norm / 0.22f));
                    crouchEnv = u;
                }
                else if (norm < 0.56f)
                {
                    // Brief contact grasp beat right at the shelf item / object
                    handPos = targetPos;
                    weight = 1f;
                    crouchEnv = 1f;
                }
                else
                {
                    // Pull grasped item cleanly back toward chest (seamless handoff to PlayerShoppingCart inspect!)
                    float u = Mathf.SmoothStep(0f, 1f, (norm - 0.56f) / 0.44f);
                    handPos = Vector3.Lerp(targetPos, chestPos, u);
                    weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - 0.84f) / 0.16f));
                    crouchEnv = 1f - u;
                }

                hum.SetLookAtPoint(true, handPos);

                if (!fired && norm >= 0.48f)
                {
                    fired = true;
                    GameFeelAudio.PlayUiClick();
                    triggerClimax();
                }

                float spineLean = (7f + 16f * lowCrouch * crouchEnv) * weight;
                float headPitch = (6f + 18f * lowCrouch * crouchEnv) * weight;
                float hipsDrop = 0.16f * lowCrouch * crouchEnv * weight;
                hum.SetDualHandAction(true, Vector3.zero, handPos, 0f, weight, spineLean, headPitch, hipsDrop);
                yield return null;
            }
        }

        private IEnumerator RunWakeStretch(PlayerModeController mode, ProceduralHumanoid hum, Action triggerClimax)
        {
            const float total = 1.25f;
            float t = 0f;

            while (t < total)
            {
                if (mode == null || mode.CurrentMode != ControlMode.OnFoot) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);

                // Smooth rise (0..0.30) -> open overhead stretch with gentle sway (0.30..0.68) -> relaxed exhale lower (0.68..1.0)
                float stretch = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(norm / 0.30f)) *
                                (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - 0.68f) / 0.32f)));
                float openSpread = Mathf.Sin(Mathf.Clamp01((norm - 0.25f) / 0.50f) * Mathf.PI) * 0.10f;

                Transform tr = _foot.transform;
                Vector3 basePos = tr.position;
                Vector3 leftUp = basePos + tr.forward * 0.06f - tr.right * (0.32f + openSpread) + Vector3.up * (1.88f + stretch * 0.08f);
                Vector3 rightUp = basePos + tr.forward * 0.06f + tr.right * (0.32f + openSpread) + Vector3.up * (1.88f + stretch * 0.08f);

                if (norm >= 0.5f)
                {
                    triggerClimax();
                }

                hum.SetDualHandAction(true, leftUp, rightUp, stretch, stretch, -11f * stretch, -12f * stretch, 0f);
                yield return null;
            }
        }

        private IEnumerator RunFarmPlantAndDig(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 2.20f;
            float t = 0f;
            bool fired = false;
            int lastDigBurst = -1;
            Color soilCol = new Color(0.46f, 0.30f, 0.16f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;

                float soilY = target != null
                    ? Mathf.Clamp(target.position.y + 0.08f, basePos.y + 0.18f, basePos.y + 0.34f)
                    : basePos.y + 0.24f;
                Vector3 soil = new Vector3(basePos.x, soilY, basePos.z) + fwd * 0.50f;

                Vector3 lSide = basePos + fwd * 0.14f - right * 0.25f + Vector3.up * 0.86f;
                Vector3 rSide = basePos + fwd * 0.14f + right * 0.25f + Vector3.up * 0.86f;
                Vector3 lKneeBrace = basePos + fwd * 0.30f - right * 0.15f + Vector3.up * 0.48f;
                Vector3 rDigReady = soil + right * 0.06f + Vector3.up * 0.16f - fwd * 0.04f;
                Vector3 lPouch = basePos + fwd * 0.05f - right * 0.22f + Vector3.up * 0.56f;
                Vector3 lAboveHole = soil - right * 0.03f + Vector3.up * 0.14f;
                Vector3 lSeedDrop = lAboveHole - Vector3.up * 0.05f;
                Vector3 rCoverStart = soil + right * 0.18f + Vector3.up * 0.09f;
                Vector3 lSwept = soil - right * 0.08f + Vector3.up * 0.08f;
                Vector3 rSwept = soil + right * 0.08f + Vector3.up * 0.08f;

                Vector3 lHand;
                Vector3 rHand;
                float lw = 1f;
                float rw = 1f;
                float spineLean;
                float headPitch;
                float hipsDrop;

                if (norm < 0.18f)
                {
                    // Phase a (0.00–0.18): kneel smoothly onto right knee, left hand moves to brace front knee, right to soil
                    float u = Mathf.SmoothStep(0f, 1f, norm / 0.18f);
                    hum.SetKneel(u, rightKneeDown: true);
                    lw = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(norm / 0.11f));
                    rw = lw;
                    lHand = Vector3.Lerp(lSide, lKneeBrace, u);
                    rHand = Vector3.Lerp(rSide, rDigReady, u);
                    spineLean = Mathf.Lerp(0f, 28f, u);
                    headPitch = Mathf.Lerp(0f, 30f, u);
                    hipsDrop = Mathf.Lerp(0f, 0.04f, u);
                }
                else if (norm < 0.55f)
                {
                    // Phase b (0.18–0.55): 3 scooping dig strokes with right hand; left hand braces on left knee
                    hum.SetKneel(1f, rightKneeDown: true);
                    float digNorm = (norm - 0.18f) / 0.37f;
                    float strokeF = digNorm * 3f;
                    int strokeIdx = Mathf.Min(2, Mathf.FloorToInt(strokeF));
                    float s = strokeF - strokeIdx;

                    Vector3 rDigBottom = soil + fwd * 0.06f - Vector3.up * 0.01f;
                    Vector3 rDigToss = soil + right * 0.22f + Vector3.up * 0.16f - fwd * 0.02f;

                    if (s < 0.28f)
                    {
                        float u = Mathf.SmoothStep(0f, 1f, s / 0.28f);
                        rHand = Vector3.Lerp(rDigReady, rDigBottom, u);
                    }
                    else if (s < 0.65f)
                    {
                        float u = Mathf.SmoothStep(0f, 1f, (s - 0.28f) / 0.37f);
                        rHand = Vector3.Lerp(rDigBottom, rDigToss, u)
                            + fwd * (0.04f * Mathf.Sin(u * Mathf.PI))
                            - Vector3.up * (0.03f * Mathf.Sin(u * Mathf.PI));
                    }
                    else
                    {
                        float u = Mathf.SmoothStep(0f, 1f, (s - 0.65f) / 0.35f);
                        rHand = Vector3.Lerp(rDigToss, rDigReady, u);
                    }

                    if (s >= 0.28f && strokeIdx > lastDigBurst)
                    {
                        lastDigBurst = strokeIdx;
                        GameFeelAudio.SpawnBurst(soil + Vector3.up * 0.04f, soilCol, count: 5, spread: 0.24f);
                    }

                    float effort = Mathf.Sin(s * Mathf.PI);
                    lHand = lKneeBrace - right * (0.014f * effort) - Vector3.up * (0.010f * effort);
                    spineLean = 28f + 6f * effort;
                    headPitch = 30f + 4f * (1f - Mathf.Clamp01((rHand.y - soil.y) / 0.20f));
                    hipsDrop = 0.04f + 0.02f * effort;
                }
                else if (norm < 0.72f)
                {
                    // Phase c (0.55–0.72): left hand fetches seed from hip pouch and drops into hole
                    hum.SetKneel(1f, rightKneeDown: true);
                    float p = (norm - 0.55f) / 0.17f;
                    if (p < 0.35f)
                    {
                        float u = Mathf.SmoothStep(0f, 1f, p / 0.35f);
                        lHand = Vector3.Lerp(lKneeBrace, lPouch, u);
                    }
                    else if (p < 0.72f)
                    {
                        float u = Mathf.SmoothStep(0f, 1f, (p - 0.35f) / 0.37f);
                        lHand = Vector3.Lerp(lPouch, lAboveHole, u) + Vector3.up * (0.04f * Mathf.Sin(u * Mathf.PI));
                    }
                    else
                    {
                        float u = Mathf.SmoothStep(0f, 1f, (p - 0.72f) / 0.28f);
                        lHand = Vector3.Lerp(lAboveHole, lSeedDrop, u);
                    }

                    rHand = Vector3.Lerp(rDigReady, rCoverStart, Mathf.SmoothStep(0f, 1f, p));
                    spineLean = Mathf.Lerp(28f, 31f, Mathf.SmoothStep(0f, 1f, p));
                    headPitch = Mathf.Lerp(30f, 34f, Mathf.SmoothStep(0f, 1f, p));
                    hipsDrop = Mathf.Lerp(0.04f, 0.05f, Mathf.SmoothStep(0f, 1f, p));

                    if (!fired && norm >= 0.66f)
                    {
                        fired = true;
                        GameFeelAudio.SpawnBurst(soil, soilCol, count: 10, spread: 0.36f);
                        triggerClimax();
                    }
                }
                else if (norm < 0.86f)
                {
                    // Phase d (0.72–0.86): both hands sweep soil inward over seed then pat twice
                    hum.SetKneel(1f, rightKneeDown: true);
                    float p = (norm - 0.72f) / 0.14f;
                    if (p < 0.45f)
                    {
                        float u = Mathf.SmoothStep(0f, 1f, p / 0.45f);
                        float arc = Mathf.Sin(u * Mathf.PI);
                        lHand = Vector3.Lerp(lSeedDrop, lSwept, u) - right * (0.10f * arc) - Vector3.up * (0.02f * arc);
                        rHand = Vector3.Lerp(rCoverStart, rSwept, u) - Vector3.up * (0.02f * arc);
                    }
                    else
                    {
                        float patT = (p - 0.45f) / 0.55f;
                        float cycle = Mathf.Repeat(patT * 2f, 1f);
                        float patY = Mathf.Sin(cycle * Mathf.PI * 2f) * 0.055f;
                        lHand = lSwept + Vector3.up * patY;
                        rHand = rSwept + Vector3.up * patY;
                    }

                    spineLean = 31f - 2f * Mathf.Abs(Mathf.Sin(p * Mathf.PI * 2f));
                    headPitch = Mathf.Lerp(34f, 32f, p);
                    hipsDrop = 0.05f;
                }
                else
                {
                    // Phase e (0.86–1.00): smooth stand-up recovery with slight relieving back arch
                    float u = Mathf.SmoothStep(0f, 1f, (norm - 0.86f) / 0.14f);
                    hum.SetKneel(1f - u, rightKneeDown: true);
                    float pushOff = Mathf.Sin(u * Mathf.PI);
                    lHand = Vector3.Lerp(lSwept, lSide, u) - fwd * (0.06f * pushOff);
                    rHand = Vector3.Lerp(rSwept, rSide, u);
                    float arch = Mathf.Sin(Mathf.Clamp01((u - 0.42f) / 0.58f) * Mathf.PI);
                    spineLean = Mathf.Lerp(31f, 0f, u) - 6f * arch;
                    headPitch = Mathf.Lerp(32f, 0f, u) - 3f * arch;
                    hipsDrop = Mathf.Lerp(0.05f, 0f, u);
                    float handFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.55f) / 0.45f));
                    lw = handFade;
                    rw = handFade;
                }

                hum.SetDualHandAction(true, lHand, rHand, lw, rw, spineLean, headPitch, hipsDrop);
                yield return null;
            }

            hum.SetKneel(0f);
        }

        private IEnumerator RunFarmWatering(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.90f;
            float t = 0f;
            bool fired = false;
            float splashTimer = 0.12f;

            hum.SetKneel(0f);
            _heldProp = SpawnActionProp(hum.RightForearm, PlayerActionKind.FarmWatering);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                float dt = Time.deltaTime;
                t += dt;
                float norm = Mathf.Clamp01(t / total);
                float env = EdgeBlend(norm, 0.12f, 0.86f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;

                float canTilt;
                float sweep;
                float liftY;
                float fwdReach;
                float recoverArch = 0f;

                if (t < 0.25f)
                {
                    // Phase 1 (0.00–0.25 s): anticipation lift & tilt 0 -> 38 deg toward left bed edge
                    float u = Mathf.SmoothStep(0f, 1f, t / 0.25f);
                    float antic = Mathf.Sin(Mathf.Clamp01(t / 0.10f) * Mathf.PI) * 0.04f;
                    canTilt = 38f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.04f) / 0.21f));
                    sweep = Mathf.Lerp(0f, -1f, u);
                    liftY = Mathf.Lerp(0.76f, 0.90f, u) + antic;
                    fwdReach = Mathf.Lerp(0.40f, 0.56f, u) - antic * 0.6f;
                }
                else if (t < 1.45f)
                {
                    // Phase 2 (0.25–1.45 s): 2 slow sweeping passes left -> right -> left across bed
                    float pourU = (t - 0.25f) / 1.20f;
                    sweep = pourU < 0.5f
                        ? Mathf.SmoothStep(-1f, 1f, pourU * 2f)
                        : Mathf.SmoothStep(1f, -1f, (pourU - 0.5f) * 2f);
                    canTilt = 38f + Mathf.Sin(pourU * Mathf.PI * 2f) * 2.5f;
                    liftY = 0.90f - 0.025f * Mathf.Sin(pourU * Mathf.PI);
                    fwdReach = 0.54f + 0.02f * Mathf.Cos(pourU * Mathf.PI * 2f);
                }
                else if (t < 1.68f)
                {
                    // Phase 3 (1.45–1.68 s): level can back (tilt 38 -> 0) and recenter
                    float u = Mathf.SmoothStep(0f, 1f, (t - 1.45f) / 0.23f);
                    canTilt = Mathf.Lerp(38f, 0f, u);
                    sweep = Mathf.Lerp(-1f, 0f, u);
                    liftY = Mathf.Lerp(0.90f, 0.86f, u);
                    fwdReach = Mathf.Lerp(0.56f, 0.48f, u);
                }
                else
                {
                    // Phase 4 (1.68–1.90 s): lower can smoothly with subtle recovery arch
                    float u = Mathf.SmoothStep(0f, 1f, (t - 1.68f) / 0.22f);
                    canTilt = 0f;
                    sweep = 0f;
                    liftY = Mathf.Lerp(0.86f, 0.74f, u);
                    fwdReach = Mathf.Lerp(0.48f, 0.36f, u);
                    recoverArch = Mathf.Sin(u * Mathf.PI);
                }

                // Weight shifts 0.03 m toward sweep side via hand targets and torso lean
                float shiftX = sweep * 0.03f;
                Vector3 rHand = basePos + fwd * fwdReach + right * (0.14f + sweep * 0.24f + shiftX) + Vector3.up * liftY;

                _heldPropLocalRot = Quaternion.Euler(canTilt, sweep * 18f, -sweep * 6f);
                Quaternion canWorldRot = tr.rotation * _heldPropLocalRot;
                // Left hand supports the front/top of the can at weight 0.85
                Vector3 lHand = rHand + canWorldRot * new Vector3(-0.05f, 0.01f, 0.12f);

                splashTimer -= dt;
                if (splashTimer <= 0f && canTilt > 20f)
                {
                    splashTimer = 0.18f;
                    Vector3 spoutTip = _heldProp != null
                        ? _heldProp.transform.TransformPoint(new Vector3(0f, 0.01f, 0.40f))
                        : (rHand + fwd * 0.26f - Vector3.up * 0.22f);
                    GameFeelAudio.SpawnBurst(spoutTip, new Color(0.32f, 0.82f, 1.0f), count: 7, spread: 0.26f);
                }

                if (!fired && norm >= 0.52f)
                {
                    fired = true;
                    GameFeelAudio.PlayWaterSplash();
                    triggerClimax();
                }

                float pourW = Mathf.Clamp01(canTilt / 38f);
                float breath = Mathf.Sin(t * 2.2f) * 0.008f;
                float spineLean = Mathf.Lerp(6f, 20f, pourW) * env - 4f * recoverArch;
                float headPitch = Mathf.Lerp(8f, 22f, pourW) * env;
                float hipsDrop = (0.04f + 0.045f * pourW + Mathf.Abs(sweep) * 0.02f + breath) * env;

                hum.SetDualHandAction(true, lHand, rHand, env * 0.85f, env, spineLean, headPitch, hipsDrop);
                yield return null;
            }

            hum.SetKneel(0f);
        }

        private IEnumerator RunFarmHarvest(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.90f;
            float t = 0f;
            bool fired = false;
            int spawnedCycle = -1;
            int droppedCycle = -1;
            GameObject activeProduce = null;

            // Left hand cradles a woven wicker harvest basket while right hand plucks ripe produce into it!
            _heldLeftProp = SpawnWickerBasket(hum.LeftForearm, withEggSlot: false, out _);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material produceMat = MakeSimpleMat(urpLit, new Color(0.92f, 0.30f, 0.12f), 0.45f, 0.04f);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;

                float cropY = target != null
                    ? Mathf.Clamp(target.position.y + 0.16f, basePos.y + 0.28f, basePos.y + 0.54f)
                    : basePos.y + 0.38f;
                Vector3 bed = new Vector3(basePos.x, cropY, basePos.z) + fwd * 0.52f;
                Vector3 crop0 = bed - right * 0.08f;
                Vector3 crop1 = bed + right * 0.10f;

                Vector3 basketHigh = basePos + fwd * 0.30f - right * 0.20f + Vector3.up * 0.94f;
                Vector3 basketLow = basePos + fwd * 0.30f - right * 0.22f + Vector3.up * 0.58f;
                Vector3 rSide = basePos + fwd * 0.22f + right * 0.24f + Vector3.up * 0.86f;
                Vector3 rReady = crop0 + Vector3.up * 0.14f - fwd * 0.06f;

                Vector3 lHand;
                Vector3 rHand;
                float lw = 1f;
                float rw = 1f;
                float spineLean;
                float headPitch;
                float hipsDrop;

                if (t < 0.26f)
                {
                    // Phase 0 (0.00–0.26 s): kneel smoothly to 0.8 while lowering wicker basket
                    float u = Mathf.SmoothStep(0f, 1f, t / 0.26f);
                    hum.SetKneel(0.8f * u, rightKneeDown: true);
                    lw = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.16f));
                    rw = lw;
                    lHand = Vector3.Lerp(basketHigh, basketLow, u);
                    rHand = Vector3.Lerp(rSide, rReady, u);
                    spineLean = Mathf.Lerp(0f, 22f, u);
                    headPitch = Mathf.Lerp(0f, 28f, u);
                    hipsDrop = Mathf.Lerp(0f, 0.03f, u);
                }
                else if (t < 1.62f)
                {
                    // Phases 1 & 2 (0.26–1.62 s): 2 crisp pluck cycles (0.68 s each)
                    hum.SetKneel(0.8f, rightKneeDown: true);
                    float workT = t - 0.26f;
                    int cycleIdx = workT < 0.68f ? 0 : 1;
                    float ct = workT - cycleIdx * 0.68f;
                    if (activeProduce != null && droppedCycle < cycleIdx - 1)
                    {
                        droppedCycle = cycleIdx - 1;
                        if (_heldLeftProp != null)
                        {
                            activeProduce.transform.SetParent(_heldLeftProp.transform, false);
                            activeProduce.transform.localPosition = new Vector3(-0.045f, -0.132f, 0.01f);
                            activeProduce.transform.localRotation = Quaternion.identity;
                            activeProduce.transform.localScale = Vector3.one;
                        }
                        else Destroy(activeProduce);
                        activeProduce = null;
                    }
                    Vector3 cropPos = cycleIdx == 0 ? crop0 : crop1;
                    lHand = basketLow + Vector3.up * (Mathf.Sin(t * 2.4f) * 0.008f);
                    Vector3 basketDropPos = lHand + Vector3.up * 0.06f + right * 0.04f;
                    Vector3 cycleStart = cycleIdx == 0 ? rReady : basketDropPos;
                    Vector3 tugPos = cropPos - fwd * 0.07f + Vector3.up * 0.05f;

                    float counterLean = 0f;
                    if (ct < 0.16f)
                    {
                        // Reach to produce
                        float u = Mathf.SmoothStep(0f, 1f, ct / 0.16f);
                        rHand = Vector3.Lerp(cycleStart, cropPos, u);
                    }
                    else if (ct < 0.24f)
                    {
                        // Grip pause (0.08 s) + attach produce orb to right forearm
                        if (spawnedCycle < cycleIdx)
                        {
                            spawnedCycle = cycleIdx;
                            if (hum.RightForearm != null)
                            {
                                activeProduce = new GameObject($"HarvestProduce_{cycleIdx}");
                                activeProduce.transform.SetParent(hum.RightForearm, false);
                                activeProduce.transform.localPosition = new Vector3(0f, -0.31f, 0.025f);
                                activeProduce.transform.localRotation = Quaternion.identity;
                                CreatePropPart("ProduceOrb", PrimitiveType.Sphere, activeProduce.transform,
                                    Vector3.zero, Vector3.one * 0.07f, produceMat);
                                _extraProps.Add(activeProduce);
                            }
                        }
                        rHand = cropPos;
                    }
                    else if (ct < 0.36f)
                    {
                        // Twist stem (sideways jitter 0.03 m simulating ±20 deg forearm yaw)
                        float u = (ct - 0.24f) / 0.12f;
                        rHand = cropPos + right * (Mathf.Sin(u * Mathf.PI * 2f) * 0.03f);
                        counterLean = 3f * Mathf.Sin(u * Mathf.PI);
                    }
                    else if (ct < 0.46f)
                    {
                        // Sharp tug back 0.07 m with counter spine lean (-5 deg)
                        float u = Mathf.SmoothStep(0f, 1f, (ct - 0.36f) / 0.10f);
                        rHand = Vector3.Lerp(cropPos, tugPos, u);
                        counterLean = -5f * Mathf.Sin(u * Mathf.PI);
                    }
                    else
                    {
                        // Carry produce smoothly into wicker basket
                        float u = Mathf.SmoothStep(0f, 1f, (ct - 0.46f) / 0.22f);
                        rHand = Vector3.Lerp(tugPos, basketDropPos, u) + Vector3.up * (0.10f * Mathf.Sin(u * Mathf.PI));
                        if (ct >= 0.62f && droppedCycle < cycleIdx)
                        {
                            droppedCycle = cycleIdx;
                            if (activeProduce != null)
                            {
                                if (_heldLeftProp != null)
                                {
                                    activeProduce.transform.SetParent(_heldLeftProp.transform, false);
                                    activeProduce.transform.localPosition = new Vector3(
                                        cycleIdx == 0 ? -0.045f : 0.048f,
                                        -0.132f,
                                        cycleIdx == 0 ? 0.01f : 0.035f);
                                    activeProduce.transform.localRotation = Quaternion.identity;
                                    activeProduce.transform.localScale = Vector3.one;
                                }
                                else
                                {
                                    Destroy(activeProduce);
                                }
                                activeProduce = null;
                            }
                            GameFeelAudio.PlayEggPickup();
                            if (!fired && cycleIdx == 1)
                            {
                                fired = true;
                                triggerClimax();
                            }
                        }
                    }

                    spineLean = 22f + counterLean;
                    headPitch = Mathf.Lerp(32f, 24f, Mathf.Clamp01((rHand.y - cropY) / 0.28f));
                    hipsDrop = 0.03f;
                }
                else
                {
                    // Phase 3 (1.62–1.90 s): rise cleanly with basket and brief relieving back arch
                    if (activeProduce != null)
                    {
                        if (_heldLeftProp != null)
                        {
                            activeProduce.transform.SetParent(_heldLeftProp.transform, false);
                            activeProduce.transform.localPosition = new Vector3(0.048f, -0.132f, 0.035f);
                            activeProduce.transform.localRotation = Quaternion.identity;
                            activeProduce.transform.localScale = Vector3.one;
                        }
                        else Destroy(activeProduce);
                        activeProduce = null;
                    }
                    if (!fired)
                    {
                        fired = true;
                        triggerClimax();
                    }
                    float u = Mathf.SmoothStep(0f, 1f, (t - 1.62f) / 0.28f);
                    hum.SetKneel(0.8f * (1f - u), rightKneeDown: true);
                    Vector3 lastDropPos = basketLow + Vector3.up * 0.06f + right * 0.04f;
                    lHand = Vector3.Lerp(basketLow, basketHigh, u);
                    rHand = Vector3.Lerp(lastDropPos, basketHigh + right * 0.24f + Vector3.up * 0.02f, u);
                    float arch = Mathf.Sin(Mathf.Clamp01((u - 0.42f) / 0.58f) * Mathf.PI);
                    spineLean = Mathf.Lerp(22f, 0f, u) - 5f * arch;
                    headPitch = Mathf.Lerp(26f, 0f, u);
                    hipsDrop = Mathf.Lerp(0.03f, 0f, u);
                    float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.58f) / 0.42f));
                    lw = fadeOut;
                    rw = fadeOut;
                }

                hum.SetDualHandAction(true, lHand, rHand, lw, rw, spineLean, headPitch, hipsDrop);
                yield return null;
            }

            hum.SetKneel(0f);
        }

        private static void EvaluateMilkStroke(float phase01, out float yOff, out float outOff, out float fwdOff, out float pullStrength)
        {
            if (phase01 < 0.22f)
            {
                // Squeeze & grip upward (+0.02 m)
                float u = Mathf.SmoothStep(0f, 1f, phase01 / 0.22f);
                yOff = 0.02f * u;
                outOff = -0.010f * u;
                fwdOff = 0f;
                pullStrength = 0f;
            }
            else if (phase01 < 0.62f)
            {
                // Downward ease-in pull (-0.07 m from top -> -0.05 m below center; total 0.07 m downward travel)
                float pu = (phase01 - 0.22f) / 0.40f;
                float easeIn = pu * pu;
                yOff = Mathf.Lerp(0.02f, -0.05f, easeIn);
                outOff = Mathf.Lerp(-0.010f, 0.008f, easeIn);
                fwdOff = 0.022f * easeIn;
                pullStrength = Mathf.Sin(pu * Mathf.PI);
            }
            else
            {
                // Release & glide smoothly back to neutral
                float ru = Mathf.SmoothStep(0f, 1f, (phase01 - 0.62f) / 0.38f);
                yOff = Mathf.Lerp(-0.05f, 0f, ru);
                outOff = Mathf.Lerp(0.008f, 0f, ru) + 0.022f * Mathf.Sin(ru * Mathf.PI);
                fwdOff = Mathf.Lerp(0.022f, 0f, ru);
                pullStrength = 0f;
            }
        }

        private IEnumerator RunMilkCow(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax, Action<float> onProgress)
        {
            const float total = 2.65f;
            const float stoolHipsDrop = 0.42f * 0.95f; // Sits hips low onto the 0.42 m milking stool
            const float milkHz = 2.2f;
            float t = 0f;
            bool fired = false;
            bool liftedPail = false;
            int lastPullBeat = -1;

            hum.SetKneel(0f);

            // Spawn 3-legged wooden milking stool + gleaming stainless-steel milk pail with rising milk level & active milk jets!
            Transform milkFill = null;
            GameObject streamL = null;
            GameObject streamR = null;
            if (_foot != null)
            {
                Transform tr0 = _foot.transform;
                Vector3 stoolPos = tr0.position - tr0.forward * 0.03f;
                stoolPos.y = tr0.position.y;
                _extraProps.Add(SpawnMilkingStool(stoolPos, tr0.rotation));

                Vector3 bucketPos = tr0.position + tr0.forward * 0.58f;
                bucketPos.y = tr0.position.y;
                _groundProp = SpawnMilkBucket(bucketPos, out milkFill, out streamL, out streamR);
            }

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                float dt = Time.deltaTime;
                t += dt;
                float norm = Mathf.Clamp01(t / total);
                hum.SetKneel(0f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;
                Vector3 udderCenter = basePos + fwd * 0.58f + Vector3.up * 0.60f;
                Vector3 udderL0 = udderCenter - right * 0.095f;
                Vector3 udderR0 = udderCenter + right * 0.095f;
                Vector3 bucketWorld = _groundProp != null ? _groundProp.transform.position : (basePos + fwd * 0.58f);

                Vector3 lSide = basePos + fwd * 0.16f - right * 0.25f + Vector3.up * 0.88f;
                Vector3 rSide = basePos + fwd * 0.16f + right * 0.25f + Vector3.up * 0.88f;
                Vector3 pailGrip = bucketWorld + Vector3.up * 0.36f;
                Vector3 lThigh = basePos + fwd * 0.24f - right * 0.18f + Vector3.up * 0.56f;

                float crouchFactor;
                float spineLean;
                float headPitch;
                float lw;
                float rw;
                Vector3 lHand;
                Vector3 rHand;

                if (norm < 0.16f)
                {
                    // Phase 1 (0.00–0.16): sit smoothly down onto wooden stool and bring hands to udder
                    float sitIn = Mathf.SmoothStep(0f, 1f, norm / 0.16f);
                    crouchFactor = stoolHipsDrop * sitIn;
                    spineLean = 24f * sitIn;
                    headPitch = 24f * sitIn;
                    lHand = Vector3.Lerp(lSide, udderL0, sitIn);
                    rHand = Vector3.Lerp(rSide, udderR0, sitIn);
                    lw = sitIn;
                    rw = sitIn;
                }
                else if (norm < 0.78f)
                {
                    // Phase 2 (0.16–0.78): alternating left/right squeeze-pull strokes at 2.2 Hz with breathing micro-motion
                    float milk01 = (norm - 0.16f) / 0.62f;
                    float milkTime = t - total * 0.16f;
                    float strokeEnv = EdgeBlend(milk01, 0.08f, 0.92f);

                    float phL = Mathf.Repeat(milkTime * milkHz, 1f);
                    float phR = Mathf.Repeat(milkTime * milkHz + 0.5f, 1f);

                    EvaluateMilkStroke(phL, out float yL, out float outL, out float fwdL, out float pullL);
                    EvaluateMilkStroke(phR, out float yR, out float outR, out float fwdR, out float pullR);

                    lHand = udderL0 + (-right * outL + Vector3.up * yL + fwd * fwdL) * strokeEnv;
                    rHand = udderR0 + (right * outR + Vector3.up * yR + fwd * fwdR) * strokeEnv;

                    float breath = Mathf.Sin(t * 2.1f) * strokeEnv;
                    crouchFactor = stoolHipsDrop + breath * 0.006f;
                    spineLean = 24f + breath * 1.4f + (pullL - pullR) * 1.6f * strokeEnv;
                    headPitch = 24f + breath * 1.2f;
                    lw = 1f;
                    rw = 1f;

                    // Update rising white milk surface inside the steel pail (empty -> brimming!)
                    if (milkFill != null)
                    {
                        float fillH = Mathf.Lerp(0.03f, 0.295f, milk01);
                        milkFill.localPosition = new Vector3(0f, fillH * 0.5f + 0.02f, 0f);
                        milkFill.localScale = new Vector3(0.248f, Mathf.Max(0.01f, fillH * 0.5f), 0.248f);
                    }

                    // Show active white 3D milk stream per hand strictly on its downward pull phase
                    bool leftPulling = milk01 > 0.03f && milk01 < 0.97f && pullL > 0.08f;
                    bool rightPulling = milk01 > 0.03f && milk01 < 0.97f && pullR > 0.08f;
                    if (streamL != null)
                    {
                        streamL.SetActive(leftPulling);
                        if (leftPulling)
                        {
                            Vector3 top = lHand - Vector3.up * 0.04f;
                            Vector3 bot = bucketWorld + Vector3.up * Mathf.Lerp(0.08f, 0.30f, milk01) - right * 0.04f;
                            PositionCylinderBetween(streamL.transform, top, bot, Mathf.Lerp(0.008f, 0.016f, pullL));
                        }
                    }
                    if (streamR != null)
                    {
                        streamR.SetActive(rightPulling);
                        if (rightPulling)
                        {
                            Vector3 top = rHand - Vector3.up * 0.04f;
                            Vector3 bot = bucketWorld + Vector3.up * Mathf.Lerp(0.08f, 0.30f, milk01) + right * 0.04f;
                            PositionCylinderBetween(streamR.transform, top, bot, Mathf.Lerp(0.008f, 0.016f, pullR));
                        }
                    }

                    // Rhythmic milk-into-tin-pail squirt sound + frothy bucket splash on every alternating pull beat!
                    int beat = Mathf.FloorToInt(milkTime * milkHz * 2f);
                    if (milk01 > 0.04f && beat != lastPullBeat)
                    {
                        lastPullBeat = beat;
                        GameFeelAudio.PlayMilkSquirt(milk01);
                        GameFeelAudio.SpawnBurst(
                            bucketWorld + Vector3.up * Mathf.Lerp(0.14f, 0.32f, milk01),
                            new Color(0.97f, 0.99f, 1.0f), count: 5, spread: 0.16f);
                        onProgress?.Invoke(milk01);
                    }
                }
                else
                {
                    if (streamL != null) streamL.SetActive(false);
                    if (streamR != null) streamR.SetActive(false);

                    if (!fired)
                    {
                        fired = true;
                        onProgress?.Invoke(1f);
                        triggerClimax();
                    }

                    if (norm < 0.85f)
                    {
                        // Phase 3a (0.78–0.85): reach cleanly down to grip pail handle while bracing left hand on thigh
                        float reachU = Mathf.SmoothStep(0f, 1f, (norm - 0.78f) / 0.07f);
                        rHand = Vector3.Lerp(udderR0, pailGrip, reachU);
                        lHand = Vector3.Lerp(udderL0, lThigh, reachU);
                        crouchFactor = stoolHipsDrop;
                        spineLean = Mathf.Lerp(24f, 26f, reachU);
                        headPitch = Mathf.Lerp(24f, 22f, reachU);
                        lw = 1f;
                        rw = 1f;
                    }
                    else
                    {
                        // Transfer the full gleaming milk pail into right hand and stand up cleanly!
                        if (!liftedPail && _groundProp != null)
                        {
                            liftedPail = true;
                            _heldProp = _groundProp;
                            _groundProp = null;
                            for (int c = 0; c < _heldProp.transform.childCount; c++)
                            {
                                _heldProp.transform.GetChild(c).localPosition += new Vector3(0f, -0.34f, 0.03f);
                            }
                        }

                        float stand01 = Mathf.SmoothStep(0f, 1f, (norm - 0.85f) / 0.15f);
                        float arch = Mathf.Sin(Mathf.Clamp01((stand01 - 0.50f) / 0.50f) * Mathf.PI);
                        crouchFactor = stoolHipsDrop * (1f - stand01);
                        spineLean = Mathf.Lerp(26f, 0f, stand01) - 4f * arch;
                        headPitch = Mathf.Lerp(22f, 4f, stand01) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((stand01 - 0.65f) / 0.35f)));
                        rHand = Vector3.Lerp(pailGrip, basePos + fwd * 0.34f + right * 0.22f + Vector3.up * 0.90f, stand01);
                        lHand = Vector3.Lerp(lThigh, basePos + fwd * 0.18f - right * 0.24f + Vector3.up * 0.94f, stand01);
                        float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((stand01 - 0.68f) / 0.32f));
                        lw = fadeOut * 0.65f;
                        rw = fadeOut;
                    }
                }

                hum.SetDualHandAction(true, lHand, rHand, lw, rw, spineLean, headPitch, crouchFactor);
                yield return null;
            }

            hum.SetKneel(0f);
        }

        private static Vector3 EvaluateSheepFlankPoint(Vector3 flankCenter, Vector3 fwd, Vector3 right, int strokeIdx, float u)
        {
            float lat = (-0.16f + strokeIdx * 0.16f) + 0.04f * u;
            float yArc = Mathf.Lerp(0.15f, -0.11f, u);
            float zArc = -0.06f - 0.08f * Mathf.Sin(u * Mathf.PI);
            return flankCenter + right * lat + Vector3.up * yArc + fwd * zArc;
        }

        private IEnumerator RunShearSheep(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax, Action<float> onProgress)
        {
            const float total = 2.55f;
            float t = 0f;
            bool fired = false;
            int lastSnipBeat = -1;

            // Articulated 2-blade steel sheep shears in Right Hand + growing fluffy wool pile on grass!
            Transform bladeL = null;
            Transform bladeR = null;
            _heldProp = SpawnArticulatedShears(hum.RightForearm, out bladeL, out bladeR);

            GameObject woolPile = null;
            if (_foot != null)
            {
                Vector3 pilePos = _foot.transform.position + _foot.transform.forward * 0.36f + _foot.transform.right * 0.42f;
                pilePos.y = _foot.transform.position.y;
                woolPile = SpawnGrowingWoolPile(pilePos);
                _extraProps.Add(woolPile);
            }

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                float dt = Time.deltaTime;
                t += dt;
                float norm = Mathf.Clamp01(t / total);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;
                Vector3 flankCenter = basePos + fwd * 0.58f + Vector3.up * 0.66f;

                Vector3 lSide = basePos + fwd * 0.18f - right * 0.24f + Vector3.up * 0.90f;
                Vector3 rSide = basePos + fwd * 0.20f + right * 0.24f + Vector3.up * 0.90f;
                Vector3 lBrace = flankCenter - right * 0.24f + Vector3.up * 0.08f - fwd * 0.06f;
                Vector3 stroke0Start = EvaluateSheepFlankPoint(flankCenter, fwd, right, 0, 0f);
                Vector3 shearEnd = flankCenter + right * 0.20f + Vector3.up * 0.02f - fwd * 0.14f;

                Vector3 lHand;
                Vector3 rHand;
                float lw = 1f;
                float rw = 1f;
                float spineLean;
                float headPitch;
                float hipsDrop;

                if (norm < 0.14f)
                {
                    // Phase 1 (0.00–0.14): slight kneel (0.5), brace left hand on sheep wool/neck, position shears at back
                    float u = Mathf.SmoothStep(0f, 1f, norm / 0.14f);
                    hum.SetKneel(0.5f * u, rightKneeDown: true);
                    lw = u * 0.92f;
                    rw = u;
                    lHand = Vector3.Lerp(lSide, lBrace, u);
                    rHand = Vector3.Lerp(rSide, stroke0Start, u);
                    if (bladeL != null) bladeL.localRotation = Quaternion.Euler(0f, -6f, 0f);
                    if (bladeR != null) bladeR.localRotation = Quaternion.Euler(0f,  6f, 0f);
                    _heldPropLocalRot = Quaternion.Euler(14f * u, -16f * u, 0f);
                    spineLean = 24f * u;
                    headPitch = 22f * u;
                    hipsDrop = 0.05f * u;
                }
                else if (norm < 0.80f)
                {
                    // Phase 2 (0.14–0.80): 3 long smooth curved shear strokes from back down to belly along flank
                    hum.SetKneel(0.5f, rightKneeDown: true);
                    float work01 = (norm - 0.14f) / 0.66f;
                    float strokeF = work01 * 3f;
                    int strokeIdx = Mathf.Min(2, Mathf.FloorToInt(strokeF));
                    float s = strokeF - strokeIdx;

                    float cutProgress;
                    float bladeOpen;
                    float propPitch;
                    float propYaw;

                    if (s < 0.74f)
                    {
                        cutProgress = Mathf.SmoothStep(0f, 1f, s / 0.74f);
                        rHand = EvaluateSheepFlankPoint(flankCenter, fwd, right, strokeIdx, cutProgress);
                        float snipWave = Mathf.Sin(cutProgress * Mathf.PI * 6f);
                        bladeOpen = 5f + (snipWave * 0.5f + 0.5f) * 20f;
                        propPitch = Mathf.Lerp(14f, 42f, cutProgress);
                        propYaw = (-16f + strokeIdx * 16f) + cutProgress * 8f;
                    }
                    else
                    {
                        float retU = Mathf.SmoothStep(0f, 1f, (s - 0.74f) / 0.26f);
                        Vector3 fromPt = EvaluateSheepFlankPoint(flankCenter, fwd, right, strokeIdx, 1f);
                        Vector3 toPt = strokeIdx < 2
                            ? EvaluateSheepFlankPoint(flankCenter, fwd, right, strokeIdx + 1, 0f)
                            : shearEnd;
                        rHand = Vector3.Lerp(fromPt, toPt, retU) - fwd * (0.09f * Mathf.Sin(retU * Mathf.PI));
                        cutProgress = 1f - retU;
                        bladeOpen = Mathf.Lerp(12f, 5f, retU);
                        propPitch = Mathf.Lerp(42f, 16f, retU);
                        propYaw = Mathf.Lerp((-16f + strokeIdx * 16f) + 8f, -16f + Mathf.Min(2, strokeIdx + 1) * 16f, retU);
                    }

                    // Left palm holds sheep's wool/neck steadily with breathing micro-motion
                    float breath = Mathf.Sin(t * 2.2f);
                    lHand = lBrace + Vector3.up * (breath * 0.008f) - fwd * (cutProgress * 0.015f);

                    if (bladeL != null) bladeL.localRotation = Quaternion.Euler(0f, -bladeOpen, 0f);
                    if (bladeR != null) bladeR.localRotation = Quaternion.Euler(0f,  bladeOpen, 0f);
                    _heldPropLocalRot = Quaternion.Euler(propPitch, propYaw, -10f * cutProgress);

                    // Crisp metallic snip-snip audio + tumbling 3D wool tufts + growing grass wool pile!
                    int snipBeat = Mathf.FloorToInt(work01 * 9f);
                    if (work01 > 0.03f && snipBeat != lastSnipBeat)
                    {
                        lastSnipBeat = snipBeat;
                        GameFeelAudio.PlayShearSnip();
                        GameFeelAudio.SpawnBurst(rHand + Vector3.up * 0.04f, new Color(0.97f, 0.95f, 0.89f), count: 7, spread: 0.30f);
                        onProgress?.Invoke(work01);
                    }

                    if (woolPile != null)
                    {
                        float pileScale = Mathf.Lerp(0.05f, 1.05f, work01);
                        woolPile.transform.localScale = Vector3.one * pileScale;
                    }

                    lw = 0.92f;
                    rw = 1f;
                    spineLean = 24f + cutProgress * 6f + breath * 1.0f;
                    headPitch = 22f + cutProgress * 8f;
                    hipsDrop = 0.05f + cutProgress * 0.015f;
                }
                else
                {
                    if (!fired)
                    {
                        fired = true;
                        onProgress?.Invoke(1f);
                        triggerClimax();
                    }

                    // Phase 3 (0.80–1.00): rise smoothly from slight kneel with subtle recovery back arch
                    float stand01 = Mathf.SmoothStep(0f, 1f, (norm - 0.80f) / 0.20f);
                    hum.SetKneel(0.5f * (1f - stand01), rightKneeDown: true);
                    if (bladeL != null) bladeL.localRotation = Quaternion.Euler(0f, -4f, 0f);
                    if (bladeR != null) bladeR.localRotation = Quaternion.Euler(0f,  4f, 0f);
                    _heldPropLocalRot = Quaternion.Euler(Mathf.Lerp(16f, 0f, stand01), Mathf.Lerp(16f, 0f, stand01), 0f);
                    lHand = Vector3.Lerp(lBrace, basePos + fwd * 0.26f - right * 0.20f + Vector3.up * 0.98f, stand01);
                    rHand = Vector3.Lerp(shearEnd, basePos + fwd * 0.28f + right * 0.22f + Vector3.up * 0.96f, stand01);
                    float arch = Mathf.Sin(Mathf.Clamp01((stand01 - 0.45f) / 0.55f) * Mathf.PI);
                    spineLean = Mathf.Lerp(24f, 0f, stand01) - 5f * arch;
                    headPitch = Mathf.Lerp(22f, 0f, stand01);
                    hipsDrop = 0.05f * (1f - stand01);
                    float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((stand01 - 0.58f) / 0.42f));
                    lw = fadeOut * 0.92f;
                    rw = fadeOut;
                }

                hum.SetDualHandAction(true, lHand, rHand, lw, rw, spineLean, headPitch, hipsDrop);
                yield return null;
            }

            hum.SetKneel(0f);
        }

        private IEnumerator RunCollectEgg(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax, Action<float> onProgress)
        {
            const float total = 2.10f;
            float t = 0f;
            bool grabbedEgg = false;
            bool placedInBasket = false;

            // Left hand cradles a real woven wicker village basket lined with golden straw!
            GameObject basketEggVisual = null;
            _heldLeftProp = SpawnWickerBasket(hum.LeftForearm, withEggSlot: true, out basketEggVisual);
            if (basketEggVisual != null) basketEggVisual.SetActive(false);

            // Capture the exact ground egg world coordinates before it gets hidden on handoff!
            Vector3 eggWorldPos = target != null
                ? target.position
                : (_foot.transform.position + _foot.transform.forward * 0.58f + Vector3.up * 0.10f);
            eggWorldPos.y = Mathf.Max(_foot.transform.position.y + 0.10f, eggWorldPos.y);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;

                Vector3 lBasketHigh = basePos + fwd * 0.32f - right * 0.16f + Vector3.up * 0.98f;
                Vector3 lBasketLow = basePos + fwd * 0.30f - right * 0.22f + Vector3.up * 0.56f;
                Vector3 rStart = basePos + fwd * 0.24f + right * 0.22f + Vector3.up * 0.88f;
                Vector3 eggTouch = eggWorldPos + Vector3.up * 0.05f;

                // Continuous kneel profile: kneel down (0..0.56 s), hold during careful pick & transfer, rise (1.28..1.90 s)
                float kneelAmt;
                if (t < 0.56f)
                {
                    kneelAmt = Mathf.SmoothStep(0f, 1f, t / 0.56f);
                }
                else if (t < 1.28f)
                {
                    kneelAmt = 1f;
                }
                else
                {
                    kneelAmt = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.28f) / 0.62f));
                }
                hum.SetKneel(kneelAmt, rightKneeDown: true);

                float rise01 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.28f) / 0.62f));
                Vector3 lHand;
                Vector3 rHand;
                float lw = 1f;
                float rw = 1f;
                float spineLean;
                float headPitch;
                float hipsDrop;

                if (t < 0.56f)
                {
                    // Phase 1 (0.00–0.56 s): kneel down (right knee down) with slower, careful approach near the egg
                    float lin = t / 0.56f;
                    float carefulApproach = Mathf.SmoothStep(0f, 1f, Mathf.Pow(lin, 0.76f));
                    lw = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.18f));
                    rw = lw;
                    lHand = Vector3.Lerp(lBasketHigh, lBasketLow, kneelAmt);
                    rHand = Vector3.Lerp(rStart, eggTouch, carefulApproach);
                    spineLean = 28f * kneelAmt;
                    headPitch = 36f * kneelAmt;
                    hipsDrop = 0.04f * kneelAmt;
                }
                else if (t < 0.71f)
                {
                    // Phase 2 (0.56–0.71 s): 0.15 s gentle careful grip pause while looking right at the egg
                    if (!grabbedEgg && t >= 0.64f)
                    {
                        grabbedEgg = true;
                        onProgress?.Invoke(0.50f);
                        _heldProp = SpawnHandheldEgg(hum.RightForearm);
                        GameFeelAudio.PlayEggPickup();
                    }
                    float micro = Mathf.Sin((t - 0.56f) * 14f) * 0.004f;
                    lHand = lBasketLow;
                    rHand = eggTouch + Vector3.up * micro;
                    spineLean = 28f;
                    headPitch = 36f;
                    hipsDrop = 0.04f;
                }
                else if (t < 1.44f)
                {
                    // Phase 3 (0.71–1.44 s): lift egg gently, look at it, and place into wicker basket
                    if (!grabbedEgg)
                    {
                        grabbedEgg = true;
                        onProgress?.Invoke(0.50f);
                        _heldProp = SpawnHandheldEgg(hum.RightForearm);
                        GameFeelAudio.PlayEggPickup();
                    }
                    float carry01 = Mathf.SmoothStep(0f, 1f, (t - 0.71f) / 0.73f);
                    lHand = Vector3.Lerp(lBasketLow, lBasketHigh, rise01);
                    Vector3 dropTarget = lHand - Vector3.up * 0.04f + fwd * 0.03f;
                    rHand = Vector3.Lerp(eggTouch, dropTarget, carry01)
                        + Vector3.up * (Mathf.Sin(carry01 * Mathf.PI) * 0.13f);
                    spineLean = Mathf.Lerp(28f, 0f, rise01);
                    headPitch = Mathf.Lerp(36f, 22f, carry01);
                    hipsDrop = Mathf.Lerp(0.04f, 0f, rise01);
                }
                else
                {
                    // Phase 4 (1.44–2.10 s): egg settles into straw basket, climax fires, rise smoothly with back arch
                    if (!placedInBasket)
                    {
                        placedInBasket = true;
                        if (_heldProp != null)
                        {
                            Destroy(_heldProp);
                            _heldProp = null;
                        }
                        if (basketEggVisual != null) basketEggVisual.SetActive(true);
                        GameFeelAudio.PlayEggPickup();
                        onProgress?.Invoke(1f);
                        triggerClimax();
                    }

                    float finish01 = Mathf.SmoothStep(0f, 1f, (t - 1.44f) / 0.66f);
                    lHand = Vector3.Lerp(lBasketLow, lBasketHigh, rise01);
                    Vector3 dropOrigin = lHand - Vector3.up * 0.04f + fwd * 0.03f;
                    Vector3 rRest = basePos + fwd * 0.28f + right * 0.18f + Vector3.up * 0.94f;
                    rHand = Vector3.Lerp(dropOrigin, rRest, finish01);
                    float arch = Mathf.Sin(Mathf.Clamp01((finish01 - 0.50f) / 0.50f) * Mathf.PI);
                    spineLean = Mathf.Lerp(28f, 0f, rise01) - 5f * arch;
                    headPitch = Mathf.Lerp(22f, 0f, finish01);
                    hipsDrop = Mathf.Lerp(0.04f, 0f, rise01);
                    float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - 0.86f) / 0.14f));
                    lw = fadeOut;
                    rw = fadeOut;
                }

                hum.SetDualHandAction(true, lHand, rHand, lw, rw, spineLean, headPitch, hipsDrop);
                yield return null;
            }

            hum.SetKneel(0f);
        }

        private static bool IsSmallFarmAnimal(Transform target)
        {
            if (target == null) return false;
            string tn = target.name.ToLowerInvariant();
            if (tn.Contains("cow") || tn.Contains("inek")) return false;
            if (tn.Contains("chicken") || tn.Contains("hen") || tn.Contains("rooster") || tn.Contains("tavuk") ||
                tn.Contains("goat") || tn.Contains("keci") || tn.Contains("keçi") ||
                tn.Contains("sheep") || tn.Contains("koyun") || tn.Contains("lamb"))
            {
                return true;
            }
            if (target.parent != null)
            {
                string pn = target.parent.name.ToLowerInvariant();
                if (pn.Contains("cow") || pn.Contains("inek")) return false;
                if (pn.Contains("chicken") || pn.Contains("hen") || pn.Contains("tavuk") ||
                    pn.Contains("goat") || pn.Contains("keci") || pn.Contains("keçi") ||
                    pn.Contains("sheep") || pn.Contains("koyun"))
                {
                    return true;
                }
            }
            return false;
        }

        private IEnumerator RunFeedAndPetAnimal(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax, Action<float> onProgress)
        {
            const float total = 2.15f;
            float t = 0f;
            bool scattered = false;
            bool fired = false;
            bool smallAnimal = IsSmallFarmAnimal(target);

            // Left hand holds a wooden farm grain bowl while right hand scatters golden feed & warmly pets the animal!
            _heldLeftProp = SpawnGrainScoop(hum.LeftForearm);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 basePos = tr.position;

                // Kneel (0.6) for small animals (chicken, goat, sheep); stay standing for cow
                float kneelAmt = smallAnimal ? 0.6f * EdgeBlend(norm, 0.15f, 0.86f) : 0f;
                hum.SetKneel(kneelAmt, rightKneeDown: true);
                float bodyYOff = -kneelAmt * 0.36f;

                float breath = Mathf.Sin(t * 2.1f) * 0.008f;
                Vector3 lHand = basePos + fwd * 0.32f - right * 0.19f + Vector3.up * (0.94f + bodyYOff + breath);
                Vector3 rSide = basePos + fwd * 0.18f + right * 0.24f + Vector3.up * (0.90f + bodyYOff);
                Vector3 rScoopBowl = lHand + right * 0.06f + Vector3.up * 0.04f;
                Vector3 rAnticBack = basePos + fwd * 0.22f + right * 0.24f + Vector3.up * (0.90f + bodyYOff);
                Vector3 rScatterEnd = basePos + fwd * 0.62f - right * 0.04f + Vector3.up * (0.92f + bodyYOff);
                Vector3 petHeadPos = basePos + fwd * 0.50f + right * 0.04f + Vector3.up * (0.92f + bodyYOff);
                Vector3 petBackPos = basePos + fwd * 0.68f + right * 0.09f + Vector3.up * (0.86f + bodyYOff);

                Vector3 rHand;
                float lw;
                float rw;
                float spineLean;
                float headPitch;
                float hipsDrop;

                if (t < 0.84f)
                {
                    // Phase 1 (0.00–0.84 s): dip into grain scoop, anticipation coil, crisp wrist scatter flick, ease to pet start
                    float p = t / 0.84f;
                    lw = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.16f));
                    rw = lw;
                    hipsDrop = smallAnimal ? 0.03f * lw : 0.05f * lw;
                    if (p < 0.32f)
                    {
                        float u = Mathf.SmoothStep(0f, 1f, p / 0.32f);
                        rHand = Vector3.Lerp(rSide, rScoopBowl, u);
                        spineLean = Mathf.Lerp(4f, 14f, u);
                        headPitch = Mathf.Lerp(4f, 16f, u);
                    }
                    else if (p < 0.52f)
                    {
                        // Anticipation draw-back before flick
                        float u = Mathf.SmoothStep(0f, 1f, (p - 0.32f) / 0.20f);
                        rHand = Vector3.Lerp(rScoopBowl, rAnticBack, u);
                        spineLean = Mathf.Lerp(14f, 8f, u);
                        headPitch = Mathf.Lerp(16f, 12f, u);
                    }
                    else if (p < 0.82f)
                    {
                        // Wrist scatter flick forward-outward across arc
                        float u = Mathf.SmoothStep(0f, 1f, (p - 0.52f) / 0.30f);
                        rHand = Vector3.Lerp(rAnticBack, rScatterEnd, u)
                            + Vector3.up * (0.09f * Mathf.Sin(u * Mathf.PI))
                            + right * (0.06f * Mathf.Sin(u * Mathf.PI));
                        spineLean = Mathf.Lerp(8f, 18f, u);
                        headPitch = Mathf.Lerp(12f, 16f, u);

                        if (!scattered && p >= 0.66f)
                        {
                            scattered = true;
                            GameFeelAudio.PlayEggPickup();
                            GameFeelAudio.SpawnBurst(rHand + fwd * 0.24f - Vector3.up * 0.14f, new Color(0.96f, 0.78f, 0.22f), count: 10, spread: 0.42f);
                            onProgress?.Invoke(0.50f);
                        }
                    }
                    else
                    {
                        // Follow-through smoothly into head petting start position
                        float u = Mathf.SmoothStep(0f, 1f, (p - 0.82f) / 0.18f);
                        rHand = Vector3.Lerp(rScatterEnd, petHeadPos, u);
                        lw = Mathf.Lerp(1f, 0.85f, u);
                        spineLean = 18f;
                        headPitch = Mathf.Lerp(16f, smallAnimal ? 22f : 16f, u);
                        hipsDrop = Mathf.Lerp(smallAnimal ? 0.03f : 0.05f, smallAnimal ? 0.03f : 0.06f, u);
                    }
                }
                else if (t < 1.86f)
                {
                    // Phase 2 (0.84–1.86 s): 3 long smooth petting strokes from head toward neck/back
                    float petU = (t - 0.84f) / 1.02f;
                    float strokeF = petU * 3f;
                    float s = strokeF - Mathf.Floor(strokeF);

                    if (s < 0.65f)
                    {
                        // Smooth stroke along head -> neck/back with slight downward pressure
                        float u = Mathf.SmoothStep(0f, 1f, s / 0.65f);
                        rHand = Vector3.Lerp(petHeadPos, petBackPos, u) - Vector3.up * (0.028f * Mathf.Sin(u * Mathf.PI));
                    }
                    else
                    {
                        // Gentle return arc above the animal's coat back to the head
                        float u = Mathf.SmoothStep(0f, 1f, (s - 0.65f) / 0.35f);
                        rHand = Vector3.Lerp(petBackPos, petHeadPos, u) + Vector3.up * (0.075f * Mathf.Sin(u * Mathf.PI));
                    }

                    // Gentle head tilt & affectionate torso sway synchronized with the strokes
                    float sway = Mathf.Sin(petU * Mathf.PI * 3f);
                    lHand += right * (sway * 0.014f);
                    lw = 0.85f;
                    rw = 1f;
                    spineLean = 18f + 4f * Mathf.Abs(sway);
                    headPitch = (smallAnimal ? 22f : 16f) + 4f * sway;
                    hipsDrop = smallAnimal ? 0.03f : 0.06f;

                    if (!fired && petU >= 0.50f)
                    {
                        fired = true;
                        onProgress?.Invoke(1f);
                        triggerClimax();
                    }
                }
                else
                {
                    // Phase 3 (1.86–2.15 s): smooth recovery & rise with brief relieving back arch
                    if (!fired)
                    {
                        fired = true;
                        onProgress?.Invoke(1f);
                        triggerClimax();
                    }
                    float u = Mathf.SmoothStep(0f, 1f, (t - 1.86f) / 0.29f);
                    rHand = Vector3.Lerp(petHeadPos, rSide, u);
                    float arch = Mathf.Sin(u * Mathf.PI);
                    spineLean = Mathf.Lerp(18f, 0f, u) - 4f * arch;
                    headPitch = Mathf.Lerp(smallAnimal ? 22f : 16f, 0f, u);
                    hipsDrop = Mathf.Lerp(smallAnimal ? 0.03f : 0.06f, 0f, u);
                    float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.45f) / 0.55f));
                    lw = fadeOut * 0.85f;
                    rw = fadeOut;
                }

                hum.SetDualHandAction(true, lHand, rHand, lw, rw, spineLean, headPitch, hipsDrop);
                yield return null;
            }

            hum.SetKneel(0f);
        }

        private IEnumerator RunGrillMangal(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.55f;
            float t = 0f;
            bool fired = false;
            float sparkTimer = 0.22f;
            Vector2 grillLocal = ResolveHomeForwardAndHeight(target, 0.56f, 0.98f, 0.42f, 0.62f, 0.90f, 1.08f, surfacePullback: -0.06f);

            _heldProp = SpawnActionProp(hum.RightForearm, PlayerActionKind.GrillMangal);

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                float dt = Time.deltaTime;
                t += dt;
                float norm = Mathf.Clamp01(t / total);
                float env = EdgeBlend(norm, 0.16f, 0.82f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 grill = tr.position + fwd * grillLocal.x + Vector3.up * grillLocal.y;
                hum.SetLookAtPoint(true, grill + fwd * 0.08f);

                // Smooth sinusoidal lateral sweep + smooth cosine arch (eliminates sharp Abs(Sin) bounce corner)
                float flip = Mathf.Sin(t * 7.5f);
                float liftArc = 0.5f - 0.5f * Mathf.Cos(t * 7.5f * 2f);
                Vector3 rHand = grill + right * (0.10f + flip * 0.15f) + Vector3.up * (liftArc * 0.095f);
                Vector3 lHand = tr.position + fwd * 0.24f - right * 0.22f + Vector3.up * 0.96f;
                _heldPropLocalRot = Quaternion.Euler(14f * env, flip * 16f, flip * 22f);

                sparkTimer -= dt;
                if (sparkTimer <= 0f && norm > 0.18f && norm < 0.82f)
                {
                    sparkTimer = 0.24f;
                    GameFeelAudio.SpawnBurst(grill + Vector3.up * 0.08f, new Color(1.0f, 0.52f, 0.14f), count: 7, spread: 0.34f);
                }

                if (!fired && norm >= 0.52f)
                {
                    fired = true;
                    triggerClimax();
                }

                hum.SetDualHandAction(true, lHand, rHand, env * 0.5f, env, 16f * env, 16f * env, 0.04f * env);
                yield return null;
            }
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

        private static void PositionCylinderBetween(Transform cyl, Vector3 a, Vector3 b, float radius)
        {
            Vector3 diff = b - a;
            float len = diff.magnitude;
            if (len < 0.005f) return;
            cyl.position = (a + b) * 0.5f;
            cyl.up = diff / len;
            cyl.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
        }

        private static GameObject SpawnMilkBucket(Vector3 groundPos, out Transform milkFill, out GameObject streamL, out GameObject streamR)
        {
            GameObject root = new GameObject("TempMilkBucket");
            root.transform.position = groundPos;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material steel = MakeSimpleMat(urpLit, new Color(0.78f, 0.82f, 0.86f), 0.76f, 0.85f);
            Material rim = MakeSimpleMat(urpLit, new Color(0.90f, 0.74f, 0.26f), 0.72f, 0.84f);
            Material milk = MakeSimpleMat(urpLit, new Color(0.99f, 0.99f, 0.97f), 0.58f, 0.0f);

            CreatePropPart("PailBody", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.17f, 0f), new Vector3(0.28f, 0.17f, 0.28f), steel);
            CreatePropPart("PailRim", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.335f, 0f), new Vector3(0.305f, 0.012f, 0.305f), rim);
            CreatePropPart("BailHandle", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.36f, 0f), new Vector3(0.28f, 0.016f, 0.016f), steel);

            GameObject fillGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fillGo.name = "MilkFill";
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            fillGo.transform.localScale = new Vector3(0.248f, 0.015f, 0.248f);
            Destroy(fillGo.GetComponent<Collider>());
            Renderer fr = fillGo.GetComponent<Renderer>();
            if (fr != null) { fr.sharedMaterial = milk; fr.shadowCastingMode = ShadowCastingMode.Off; }
            milkFill = fillGo.transform;

            streamL = CreateWorldMilkStream("MilkStreamL", milk);
            streamR = CreateWorldMilkStream("MilkStreamR", milk);
            streamL.transform.SetParent(root.transform, true);
            streamR.transform.SetParent(root.transform, true);
            streamL.SetActive(false);
            streamR.SetActive(false);
            return root;
        }

        private static GameObject CreateWorldMilkStream(string name, Material milkMat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) { r.sharedMaterial = milkMat; r.shadowCastingMode = ShadowCastingMode.Off; }
            return go;
        }

        private static GameObject SpawnMilkingStool(Vector3 groundPos, Quaternion rot)
        {
            GameObject root = new GameObject("TempMilkingStool");
            root.transform.SetPositionAndRotation(groundPos, rot);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material oak = MakeSimpleMat(urpLit, new Color(0.56f, 0.36f, 0.18f), 0.24f, 0.02f);
            Material darkWood = MakeSimpleMat(urpLit, new Color(0.42f, 0.26f, 0.12f), 0.20f, 0.02f);

            CreatePropPart("Seat", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.42f, 0f), new Vector3(0.34f, 0.030f, 0.34f), oak);
            for (int i = 0; i < 3; i++)
            {
                float a = (i / 3f) * Mathf.PI * 2f + 0.5f;
                Vector3 lp = new Vector3(Mathf.Cos(a) * 0.105f, 0.21f, Mathf.Sin(a) * 0.105f);
                Quaternion lr = Quaternion.Euler(Mathf.Sin(a) * 10f, 0f, -Mathf.Cos(a) * 10f);
                CreatePropPart($"Leg_{i}", PrimitiveType.Cylinder, root.transform,
                    lp, new Vector3(0.038f, 0.21f, 0.038f), darkWood, lr);
            }
            return root;
        }

        private static GameObject SpawnArticulatedShears(Transform rightForearm, out Transform bladeLPivot, out Transform bladeRPivot)
        {
            bladeLPivot = null;
            bladeRPivot = null;
            if (rightForearm == null) return null;

            GameObject root = new GameObject("HeldArticulatedShears");
            root.transform.position = rightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material steel = MakeSimpleMat(urpLit, new Color(0.88f, 0.90f, 0.94f), 0.88f, 0.92f);
            Material redGrip = MakeSimpleMat(urpLit, new Color(0.88f, 0.16f, 0.14f), 0.38f, 0.05f);
            Material bolt = MakeSimpleMat(urpLit, new Color(0.94f, 0.78f, 0.24f), 0.74f, 0.86f);

            CreatePropPart("GripLoopL", PrimitiveType.Cube, root.transform,
                new Vector3(-0.028f, 0f, -0.02f), new Vector3(0.028f, 0.028f, 0.14f), redGrip, Quaternion.Euler(0f, 6f, 0f));
            CreatePropPart("GripLoopR", PrimitiveType.Cube, root.transform,
                new Vector3( 0.028f, 0f, -0.02f), new Vector3(0.028f, 0.028f, 0.14f), redGrip, Quaternion.Euler(0f, -6f, 0f));
            CreatePropPart("PivotBolt", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.002f, 0.065f), new Vector3(0.032f, 0.020f, 0.032f), bolt);

            GameObject pivL = new GameObject("BladeLPivot");
            pivL.transform.SetParent(root.transform, false);
            pivL.transform.localPosition = new Vector3(0f, 0.006f, 0.065f);
            CreatePropPart("BladeL", PrimitiveType.Cube, pivL.transform,
                new Vector3(-0.012f, 0f, 0.14f), new Vector3(0.028f, 0.010f, 0.28f), steel);
            bladeLPivot = pivL.transform;

            GameObject pivR = new GameObject("BladeRPivot");
            pivR.transform.SetParent(root.transform, false);
            pivR.transform.localPosition = new Vector3(0f, -0.006f, 0.065f);
            CreatePropPart("BladeR", PrimitiveType.Cube, pivR.transform,
                new Vector3(0.012f, 0f, 0.14f), new Vector3(0.028f, 0.010f, 0.28f), steel);
            bladeRPivot = pivR.transform;

            return root;
        }

        private static GameObject SpawnGrowingWoolPile(Vector3 groundPos)
        {
            GameObject root = new GameObject("TempShearedWoolPile");
            root.transform.position = groundPos;
            root.transform.localScale = Vector3.one * 0.05f;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material wool = MakeSimpleMat(urpLit, new Color(0.97f, 0.95f, 0.90f), 0.10f, 0.0f);
            CreatePropPart("WoolBase", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.38f, 0.22f, 0.34f), wool);
            CreatePropPart("WoolPuffA", PrimitiveType.Sphere, root.transform, new Vector3(-0.14f, 0.14f, 0.08f), new Vector3(0.24f, 0.20f, 0.22f), wool);
            CreatePropPart("WoolPuffB", PrimitiveType.Sphere, root.transform, new Vector3( 0.15f, 0.15f, -0.06f), new Vector3(0.25f, 0.21f, 0.24f), wool);
            CreatePropPart("WoolTop", PrimitiveType.Sphere, root.transform, new Vector3(0.02f, 0.24f, 0.02f), new Vector3(0.26f, 0.19f, 0.25f), wool);
            return root;
        }

        private static GameObject SpawnWickerBasket(Transform leftForearm, bool withEggSlot, out GameObject eggInside)
        {
            eggInside = null;
            if (leftForearm == null) return null;

            // Anchored cleanly at HandleTop (y = 0) so Deniz's left palm grips the wicker handle naturally!
            GameObject root = new GameObject("HeldWickerBasket");
            root.transform.position = leftForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material wicker = MakeSimpleMat(urpLit, new Color(0.72f, 0.50f, 0.24f), 0.22f, 0.02f);
            Material rim = MakeSimpleMat(urpLit, new Color(0.56f, 0.36f, 0.16f), 0.20f, 0.02f);
            Material straw = MakeSimpleMat(urpLit, new Color(0.92f, 0.78f, 0.34f), 0.12f, 0.0f);
            Material eggMat = MakeSimpleMat(urpLit, new Color(0.98f, 0.94f, 0.84f), 0.45f, 0.0f);

            CreatePropPart("HandleTop", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.00f, 0.02f), new Vector3(0.25f, 0.024f, 0.028f), rim);
            CreatePropPart("HandleL", PrimitiveType.Cylinder, root.transform,
                new Vector3(-0.13f, -0.085f, 0.02f), new Vector3(0.022f, 0.088f, 0.022f), rim, Quaternion.Euler(0f, 0f, -10f));
            CreatePropPart("HandleR", PrimitiveType.Cylinder, root.transform,
                new Vector3( 0.13f, -0.085f, 0.02f), new Vector3(0.022f, 0.088f, 0.022f), rim, Quaternion.Euler(0f, 0f,  10f));
            CreatePropPart("BasketRim", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, -0.165f, 0.02f), new Vector3(0.305f, 0.016f, 0.285f), rim);
            CreatePropPart("BasketBody", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, -0.255f, 0.02f), new Vector3(0.28f, 0.095f, 0.26f), wicker);
            CreatePropPart("StrawNest", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, -0.185f, 0.02f), new Vector3(0.25f, 0.025f, 0.23f), straw);

            if (withEggSlot)
            {
                GameObject egg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                egg.name = "BasketEgg";
                egg.transform.SetParent(root.transform, false);
                egg.transform.localPosition = new Vector3(0f, -0.125f, 0.02f);
                egg.transform.localScale = new Vector3(0.10f, 0.135f, 0.10f);
                egg.transform.localRotation = Quaternion.Euler(22f, 18f, -14f);
                Destroy(egg.GetComponent<Collider>());
                Renderer r = egg.GetComponent<Renderer>();
                if (r != null) { r.sharedMaterial = eggMat; r.shadowCastingMode = ShadowCastingMode.Off; }
                eggInside = egg;
            }
            return root;
        }

        private static GameObject SpawnHandheldEgg(Transform rightForearm)
        {
            if (rightForearm == null) return null;
            GameObject root = new GameObject("HeldVillageEgg");
            root.transform.position = rightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material eggMat = MakeSimpleMat(urpLit, new Color(0.98f, 0.94f, 0.84f), 0.48f, 0.0f);
            CreatePropPart("EggMesh", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, -0.02f, 0.03f), new Vector3(0.095f, 0.128f, 0.095f), eggMat);
            return root;
        }

        private static GameObject SpawnGrainScoop(Transform leftForearm)
        {
            if (leftForearm == null) return null;
            GameObject root = new GameObject("HeldGrainScoop");
            root.transform.position = leftForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material wood = MakeSimpleMat(urpLit, new Color(0.60f, 0.40f, 0.20f), 0.25f, 0.02f);
            Material grain = MakeSimpleMat(urpLit, new Color(0.94f, 0.76f, 0.22f), 0.18f, 0.0f);
            CreatePropPart("Bowl", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, -0.04f, 0.04f), new Vector3(0.22f, 0.055f, 0.22f), wood);
            CreatePropPart("GrainHeap", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0.015f, 0.04f), new Vector3(0.19f, 0.075f, 0.19f), grain);
            return root;
        }

        private static GameObject SpawnActionProp(Transform rightForearm, PlayerActionKind kind)
        {
            if (rightForearm == null) return null;

            GameObject root = new GameObject($"HeldActionProp_{kind}");
            root.transform.position = rightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            root.transform.rotation = Quaternion.identity;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (kind == PlayerActionKind.UseTvRemote)
            {
                CreatePropPart("RemoteBody", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 0f, 0.05f), new Vector3(0.048f, 0.020f, 0.165f),
                    MakeSimpleMat(urpLit, new Color(0.10f, 0.11f, 0.13f), 0.35f, 0.05f));
                CreatePropPart("PowerBtn", PrimitiveType.Cube, root.transform,
                    new Vector3(-0.012f, 0.012f, 0.105f), new Vector3(0.012f, 0.008f, 0.012f),
                    MakeSimpleMat(urpLit, new Color(0.88f, 0.16f, 0.14f), 0.50f, 0f));
                CreatePropPart("ChannelPad", PrimitiveType.Cube, root.transform,
                    new Vector3(0.004f, 0.011f, 0.055f), new Vector3(0.032f, 0.006f, 0.048f),
                    MakeSimpleMat(urpLit, new Color(0.45f, 0.48f, 0.52f), 0.40f, 0.15f));
            }
            else if (kind == PlayerActionKind.RefuelCar)
            {
                CreatePropPart("NozzleGrip", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, -0.02f, 0.02f), new Vector3(0.045f, 0.12f, 0.075f),
                    MakeSimpleMat(urpLit, new Color(0.12f, 0.58f, 0.24f), 0.40f, 0.10f));
                CreatePropPart("NozzleSpout", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, 0.03f, 0.13f), new Vector3(0.026f, 0.10f, 0.026f),
                    MakeSimpleMat(urpLit, new Color(0.70f, 0.74f, 0.78f), 0.75f, 0.80f),
                    Quaternion.Euler(82f, 0f, 0f));
            }
            else if (kind == PlayerActionKind.FarmWatering)
            {
                Material canMat = MakeSimpleMat(urpLit, new Color(0.12f, 0.66f, 0.64f), 0.48f, 0.20f);
                Material brassMat = MakeSimpleMat(urpLit, new Color(0.90f, 0.74f, 0.26f), 0.70f, 0.80f);
                CreatePropPart("CanBody", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, -0.10f, 0.06f), new Vector3(0.20f, 0.13f, 0.20f), canMat);
                CreatePropPart("CanSpout", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, -0.06f, 0.24f), new Vector3(0.035f, 0.14f, 0.035f), canMat,
                    Quaternion.Euler(64f, 0f, 0f));
                CreatePropPart("CanRose", PrimitiveType.Sphere, root.transform,
                    new Vector3(0f, 0.01f, 0.36f), new Vector3(0.08f, 0.08f, 0.05f), brassMat);
            }
            else if (kind == PlayerActionKind.ShearSheep)
            {
                Material bladeMat = MakeSimpleMat(urpLit, new Color(0.84f, 0.86f, 0.90f), 0.82f, 0.88f);
                Material gripMat = MakeSimpleMat(urpLit, new Color(0.84f, 0.18f, 0.16f), 0.35f, 0.05f);
                CreatePropPart("ShearsGrip", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 0f, 0.02f), new Vector3(0.05f, 0.03f, 0.12f), gripMat);
                CreatePropPart("BladeL", PrimitiveType.Cube, root.transform,
                    new Vector3(-0.02f, 0f, 0.15f), new Vector3(0.018f, 0.012f, 0.18f), bladeMat,
                    Quaternion.Euler(0f, -8f, 0f));
                CreatePropPart("BladeR", PrimitiveType.Cube, root.transform,
                    new Vector3( 0.02f, 0f, 0.15f), new Vector3(0.018f, 0.012f, 0.18f), bladeMat,
                    Quaternion.Euler(0f,  8f, 0f));
            }
            else if (kind == PlayerActionKind.GrillMangal)
            {
                Material steel = MakeSimpleMat(urpLit, new Color(0.82f, 0.84f, 0.88f), 0.85f, 0.90f);
                CreatePropPart("TongsA", PrimitiveType.Cube, root.transform,
                    new Vector3(-0.018f, 0f, 0.16f), new Vector3(0.012f, 0.014f, 0.32f), steel,
                    Quaternion.Euler(0f, -4f, 0f));
                CreatePropPart("TongsB", PrimitiveType.Cube, root.transform,
                    new Vector3( 0.018f, 0f, 0.16f), new Vector3(0.012f, 0.014f, 0.32f), steel,
                    Quaternion.Euler(0f,  4f, 0f));
            }

            return root;
        }

        private static void CreatePropPart(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material mat,
            Quaternion? localRot = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            if (localRot.HasValue) part.transform.localRotation = localRot.Value;
            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = part.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static Material MakeSimpleMat(Shader shader, Color col, float smoothness, float metallic)
        {
            Material m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }
    }
}
