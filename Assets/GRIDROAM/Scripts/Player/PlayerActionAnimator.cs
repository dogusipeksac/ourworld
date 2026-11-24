using System;
using System.Collections;
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
        GrillMangal = 15
    }

    /// <summary>
    /// Full-body procedural 2-bone IK + posture + handheld prop animator for every home and street
    /// interaction (washing hands/face at the sink, şadırvan abdest, typing on PC, entering ATM PIN,
    /// aiming TV remote, changing outfits at wardrobe, loading washing machine, fueling car,
    /// inspecting objects, and morning stretch after waking up).
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class PlayerActionAnimator : MonoBehaviour
    {
        private PlayerFootController _foot;
        private CharacterController _cc;
        private Coroutine _activeRoutine;
        private GameObject _heldProp;
        private GameObject _groundProp;
        private Quaternion _heldPropLocalRot = Quaternion.identity;
        private bool _holdingModalLock;

        public bool IsPlaying => _activeRoutine != null;

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
            string startStatus = null)
        {
            if (mode == null || mode.FootController == null || mode.CurrentMode != ControlMode.OnFoot)
            {
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
            anim.StartAction(mode, foot, target, kind, onClimax, onFinished, startStatus);
        }

        private void Awake()
        {
            _foot = GetComponent<PlayerFootController>();
            _cc = GetComponent<CharacterController>();
        }

        private void OnDisable()
        {
            CancelActive();
        }

        private void OnDestroy()
        {
            CancelActive();
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
            string startStatus)
        {
            _foot = foot;
            if (_cc == null) _cc = foot.GetComponent<CharacterController>();
            CancelActive();
            _activeRoutine = StartCoroutine(ActionRoutine(mode, target, kind, onClimax, onFinished, startStatus));
        }

        private IEnumerator ActionRoutine(
            PlayerModeController mode,
            Transform target,
            PlayerActionKind kind,
            Action onClimax,
            Action onFinished,
            string startStatus)
        {
            ProceduralHumanoid hum = _foot != null ? _foot.Humanoid : null;
            if (hum == null || mode == null || mode.CurrentMode != ControlMode.OnFoot)
            {
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

            // Step 1: Smoothly step slightly closer if far from target & turn cleanly toward it
            if (target != null && kind != PlayerActionKind.WakeStretch)
            {
                Vector3 toTarget = target.position - _foot.transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                float idealDist = GetIdealStandDistance(kind);

                if (dist > idealDist + 0.55f && dist < 3.4f)
                {
                    Vector3 approachDir = toTarget / Mathf.Max(0.001f, dist);
                    Vector3 standPos = new Vector3(target.position.x, _foot.transform.position.y, target.position.z) - approachDir * idealDist;
                    float walkT = 0f;
                    while (walkT < 0.70f)
                    {
                        if (ShouldAbort(mode)) break;
                        Vector3 d = standPos - _foot.transform.position;
                        d.y = 0f;
                        float rem = d.magnitude;
                        if (rem <= 0.16f) break;

                        float dt = Time.deltaTime;
                        walkT += dt;
                        Vector3 dir = d / Mathf.Max(0.001f, rem);
                        _foot.transform.rotation = Quaternion.Slerp(
                            _foot.transform.rotation,
                            Quaternion.LookRotation(dir, Vector3.up),
                            1f - Mathf.Exp(-12f * dt));
                        float spd = Mathf.Min(2.4f, rem * 4.5f);
                        if (_cc != null && _cc.enabled)
                        {
                            _cc.Move((dir * spd + Vector3.down * 4f) * dt);
                        }
                        hum.SetLocomotion(spd, true, 0f, dir);
                        yield return null;
                    }
                }

                hum.SetLocomotion(0f, true, 0f, _foot.transform.forward);
                Vector3 lookDelta = target.position - _foot.transform.position;
                lookDelta.y = 0f;
                if (lookDelta.sqrMagnitude > 0.004f)
                {
                    Quaternion faceRot = Quaternion.LookRotation(lookDelta.normalized, Vector3.up);
                    float turnT = 0f;
                    while (turnT < 0.12f)
                    {
                        if (ShouldAbort(mode)) break;
                        turnT += Time.deltaTime;
                        _foot.transform.rotation = Quaternion.Slerp(_foot.transform.rotation, faceRot, 1f - Mathf.Exp(-15f * Time.deltaTime));
                        hum.SetLocomotion(0f, true, 0f, _foot.transform.forward);
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
                    yield return RunMilkCow(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.ShearSheep:
                    yield return RunShearSheep(mode, hum, target, triggerClimax);
                    break;
                case PlayerActionKind.GrillMangal:
                    yield return RunGrillMangal(mode, hum, target, triggerClimax);
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
                Vector3 basinCenter = basePos + fwd * 0.44f + Vector3.up * 0.94f;
                Vector3 faceCenter = hum.HeadTransform != null
                    ? hum.HeadTransform.position + fwd * 0.14f - Vector3.up * 0.02f
                    : basePos + fwd * 0.24f + Vector3.up * 1.46f;

                Vector3 leftTarget;
                Vector3 rightTarget;
                float spineLean;
                float headPitch;

                if (t < 0.95f)
                {
                    // Phase 1: Rubbing both hands together under the faucet
                    float rub = t * 14f;
                    leftTarget = basinCenter - right * (0.055f + Mathf.Sin(rub) * 0.035f) + fwd * (Mathf.Cos(rub) * 0.035f) + Vector3.up * (Mathf.Sin(rub * 0.5f) * 0.02f);
                    rightTarget = basinCenter + right * (0.055f - Mathf.Sin(rub) * 0.035f) - fwd * (Mathf.Cos(rub) * 0.035f) - Vector3.up * (Mathf.Sin(rub * 0.5f) * 0.02f);
                    spineLean = 24f * blend;
                    headPitch = 22f * blend;

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
                Vector3 tapPos = basePos + fwd * 0.46f + Vector3.up * 0.92f;
                Vector3 faceCenter = hum.HeadTransform != null
                    ? hum.HeadTransform.position + fwd * 0.14f - Vector3.up * 0.02f
                    : basePos + fwd * 0.24f + Vector3.up * 1.46f;

                Vector3 leftTarget;
                Vector3 rightTarget;
                float spineLean = 22f * blend;
                float headPitch = 22f * blend;

                if (t < 0.95f)
                {
                    // 1. Wash both hands
                    float rub = t * 14f;
                    leftTarget = tapPos - right * (0.055f + Mathf.Sin(rub) * 0.035f) + fwd * (Mathf.Cos(rub) * 0.03f);
                    rightTarget = tapPos + right * (0.055f - Mathf.Sin(rub) * 0.035f) - fwd * (Mathf.Cos(rub) * 0.03f);
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
                    // 3. Wash right forearm then left forearm
                    bool washRightArm = t < 2.60f;
                    float wipe = Mathf.Sin((t - 2.05f) * 11f) * 0.10f;
                    if (washRightArm)
                    {
                        rightTarget = tapPos + right * 0.04f + Vector3.up * 0.05f;
                        leftTarget = rightTarget - fwd * (0.06f + wipe) + Vector3.up * 0.03f;
                    }
                    else
                    {
                        leftTarget = tapPos - right * 0.04f + Vector3.up * 0.05f;
                        rightTarget = leftTarget - fwd * (0.06f + wipe) + Vector3.up * 0.03f;
                    }
                    spineLean = 18f * blend;
                    headPitch = 20f * blend;

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

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.12f, 0.86f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 deskPos = tr.position + fwd * 0.42f + Vector3.up * 0.96f;

                float phaseL = Mathf.Sin(t * 22f);
                float phaseR = Mathf.Sin(t * 22f + Mathf.PI);
                Vector3 leftTarget = deskPos - right * (0.14f + Mathf.Cos(t * 7f) * 0.03f) + Vector3.up * (Mathf.Max(0f, phaseL) * 0.038f);
                Vector3 rightTarget = deskPos + right * (0.14f + Mathf.Sin(t * 8f) * 0.03f) + Vector3.up * (Mathf.Max(0f, phaseR) * 0.038f);

                if (t >= nextClick && t < total - 0.35f)
                {
                    nextClick += 0.26f;
                    GameFeelAudio.PlayUiClick();
                }
                if (norm >= 0.80f)
                {
                    triggerClimax();
                }

                hum.SetDualHandAction(true, leftTarget, rightTarget, blend, blend, 12f * blend, 15f * blend, 0f);
                yield return null;
            }
        }

        private IEnumerator RunAtmKeypad(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.75f;
            float t = 0f;
            int pressedCount = 0;
            Vector2[] pinOffsets =
            {
                new Vector2(-0.05f,  0.04f),
                new Vector2( 0.04f,  0.04f),
                new Vector2(-0.02f, -0.02f),
                new Vector2( 0.05f, -0.05f)
            };

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float blend = EdgeBlend(norm, 0.12f, 0.86f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 padCenter = tr.position + fwd * 0.45f + right * 0.06f + Vector3.up * 1.24f;
                Vector3 slotPos = tr.position + fwd * 0.42f + right * 0.08f + Vector3.up * 1.08f;

                Vector3 rightTarget;
                if (t < 1.15f)
                {
                    int digitIdx = Mathf.Clamp(Mathf.FloorToInt(t / 0.28f), 0, 3);
                    float localPress = Mathf.Sin(Mathf.Clamp01((t - digitIdx * 0.28f) / 0.24f) * Mathf.PI);
                    Vector2 off = pinOffsets[digitIdx];
                    rightTarget = padCenter + right * off.x + Vector3.up * off.y + fwd * (localPress * 0.06f);

                    if (digitIdx + 1 > pressedCount && localPress > 0.72f)
                    {
                        pressedCount = digitIdx + 1;
                        GameFeelAudio.PlayUiClick();
                    }
                }
                else
                {
                    // Reach to card/cash dispenser slot & pull back
                    float grab = Mathf.Sin(Mathf.Clamp01((t - 1.15f) / 0.50f) * Mathf.PI);
                    rightTarget = Vector3.Lerp(tr.position + fwd * 0.26f + right * 0.12f + Vector3.up * 1.05f, slotPos, grab);
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
                float blend = EdgeBlend(norm, 0.16f, 0.80f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                float jab1 = Mathf.Sin(Mathf.Clamp01((t - 0.28f) / 0.22f) * Mathf.PI);
                float jab2 = Mathf.Sin(Mathf.Clamp01((t - 0.58f) / 0.22f) * Mathf.PI);
                float jab = Mathf.Max(jab1, jab2);

                Vector3 aimPos = tr.position + fwd * (0.48f + jab * 0.055f) + right * 0.12f + Vector3.up * (1.24f + jab * 0.02f);

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

                hum.SetDualHandAction(true, Vector3.zero, aimPos, 0f, blend, 4f * blend, -4f * blend, 0f);
                yield return null;
            }
        }

        private IEnumerator RunChangeOutfit(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.85f;
            float t = 0f;
            bool latchPlayed = false;

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

                Vector3 leftTarget;
                Vector3 rightTarget;
                float spineLean;
                float headPitch;

                if (t < 0.80f)
                {
                    // Stage 1: Reach forward and pull wardrobe doors wide open
                    float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.70f));
                    float spread = Mathf.Lerp(0.10f, 0.34f, open);
                    float depth = Mathf.Lerp(0.46f, 0.34f, open);
                    leftTarget = basePos + fwd * depth - right * spread + Vector3.up * 1.24f;
                    rightTarget = basePos + fwd * depth + right * spread + Vector3.up * 1.24f;
                    spineLean = 8f * blend;
                    headPitch = 4f * blend;

                    if (!latchPlayed && t >= 0.25f)
                    {
                        latchPlayed = true;
                        GameFeelAudio.PlayDoorLatch();
                    }
                }
                else
                {
                    // Stage 2: Climax jacket swap + brush/straighten jacket lapels down chest!
                    if (t >= 0.84f)
                    {
                        triggerClimax();
                    }
                    float brush = Mathf.Clamp01((t - 0.80f) / 0.85f);
                    float yBrush = Mathf.Lerp(1.36f, 1.00f, brush);
                    float sway = Mathf.Sin(brush * Mathf.PI) * 0.04f;
                    leftTarget = basePos + fwd * (0.22f + sway) - right * 0.13f + Vector3.up * yBrush;
                    rightTarget = basePos + fwd * (0.22f + sway) + right * 0.13f + Vector3.up * yBrush;
                    spineLean = -4f * blend;
                    headPitch = 16f * blend;
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

                Vector3 leftTarget;
                Vector3 rightTarget;
                float spineLean;
                float headPitch;
                float hipsDrop;

                if (t < 1.05f)
                {
                    // Stage 1: Bend down to load clothes & close washing drum
                    float push = Mathf.Sin(Mathf.Clamp01(t / 1.0f) * Mathf.PI);
                    leftTarget = basePos + fwd * (0.40f + push * 0.08f) - right * 0.11f + Vector3.up * 0.68f;
                    rightTarget = basePos + fwd * (0.40f + push * 0.08f) + right * 0.11f + Vector3.up * 0.68f;
                    spineLean = 32f * blend;
                    headPitch = 22f * blend;
                    hipsDrop = 0.11f * blend;

                    if (!drumClosed && t >= 0.72f)
                    {
                        drumClosed = true;
                        GameFeelAudio.PlayDoorLatch();
                    }
                }
                else
                {
                    // Stage 2: Rise up and press the start program dial with right hand
                    float press = Mathf.Sin(Mathf.Clamp01((t - 1.15f) / 0.50f) * Mathf.PI);
                    leftTarget = basePos + fwd * 0.24f - right * 0.18f + Vector3.up * 0.94f;
                    rightTarget = basePos + fwd * (0.42f + press * 0.06f) + right * 0.09f + Vector3.up * 1.02f;
                    spineLean = 11f * blend;
                    headPitch = 14f * blend;
                    hipsDrop = 0.02f * blend;

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

                float pulse = Mathf.Sin(t * 7f) * 0.014f;
                Vector3 rightNozzle = basePos + fwd * 0.50f + right * 0.10f + Vector3.up * (1.02f + pulse);
                Vector3 leftSupport = basePos + fwd * 0.24f - right * 0.19f + Vector3.up * 0.98f;

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

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float reach = Mathf.Sin(norm * Mathf.PI);

                Transform tr = _foot.transform;
                Vector3 handPos = tr.position + tr.forward * 0.46f + tr.right * 0.10f + Vector3.up * 1.18f;
                if (!fired && norm >= 0.48f)
                {
                    fired = true;
                    GameFeelAudio.PlayUiClick();
                    triggerClimax();
                }

                hum.SetDualHandAction(true, Vector3.zero, handPos, 0f, reach, 7f * reach, 8f * reach, 0f);
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
                float stretch = Mathf.Sin(norm * Mathf.PI);

                Transform tr = _foot.transform;
                Vector3 basePos = tr.position;
                Vector3 leftUp = basePos + tr.forward * 0.06f - tr.right * 0.34f + Vector3.up * 1.94f;
                Vector3 rightUp = basePos + tr.forward * 0.06f + tr.right * 0.34f + Vector3.up * 1.94f;

                if (norm >= 0.5f)
                {
                    triggerClimax();
                }

                hum.SetDualHandAction(true, leftUp, rightUp, stretch, stretch, -10f * stretch, -12f * stretch, 0f);
                yield return null;
            }
        }

        private IEnumerator RunFarmPlantAndDig(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.45f;
            float t = 0f;
            bool fired = false;

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float env = EdgeBlend(norm, 0.18f, 0.80f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 bed = tr.position + fwd * 0.62f + Vector3.up * 0.42f;

                float pat = Mathf.Sin(t * 11f);
                Vector3 lHand = bed - right * 0.16f + Vector3.up * (0.08f + Mathf.Max(0f, pat) * 0.12f);
                Vector3 rHand = bed + right * 0.16f + Vector3.up * (0.08f + Mathf.Max(0f, -pat) * 0.14f) + fwd * (Mathf.Sin(t * 5.5f) * 0.08f);

                if (!fired && norm >= 0.52f)
                {
                    fired = true;
                    GameFeelAudio.SpawnBurst(bed, new Color(0.46f, 0.30f, 0.16f), count: 10, spread: 0.36f);
                    triggerClimax();
                }

                hum.SetDualHandAction(true, lHand, rHand, env, env, 40f * env, 26f * env, 0.36f * env);
                yield return null;
            }
        }

        private IEnumerator RunFarmWatering(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.65f;
            float t = 0f;
            bool fired = false;
            float splashTimer = 0.22f;

            _heldProp = SpawnActionProp(hum.RightForearm, PlayerActionKind.FarmWatering);

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
                float sweep = Mathf.Sin(norm * Mathf.PI * 2.5f);

                Vector3 rHand = tr.position + fwd * 0.56f + right * (0.16f + sweep * 0.28f) + Vector3.up * (0.86f - 0.10f * env);
                Vector3 lHand = tr.position + fwd * 0.26f - right * 0.24f + Vector3.up * 0.90f;

                _heldPropLocalRot = Quaternion.Euler(38f * env, sweep * 16f, 0f);

                splashTimer -= dt;
                if (splashTimer <= 0f && norm > 0.18f && norm < 0.84f)
                {
                    splashTimer = 0.20f;
                    GameFeelAudio.SpawnBurst(rHand + fwd * 0.24f - Vector3.up * 0.32f, new Color(0.32f, 0.82f, 1.0f), count: 7, spread: 0.28f);
                }

                if (!fired && norm >= 0.52f)
                {
                    fired = true;
                    GameFeelAudio.PlayWaterSplash();
                    triggerClimax();
                }

                hum.SetDualHandAction(true, lHand, rHand, env * 0.55f, env, 22f * env, 20f * env, 0.10f * env);
                yield return null;
            }
        }

        private IEnumerator RunFarmHarvest(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.25f;
            float t = 0f;
            bool fired = false;

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / total);
                float env = EdgeBlend(norm, 0.18f, 0.82f);
                float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((norm - 0.45f) / 0.35f));

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 low = tr.position + fwd * 0.58f + Vector3.up * 0.48f;
                Vector3 high = tr.position + fwd * 0.34f + Vector3.up * 1.12f;
                Vector3 p = Vector3.Lerp(low, high, lift);

                Vector3 lHand = p - right * 0.14f;
                Vector3 rHand = p + right * 0.14f;

                if (!fired && norm >= 0.50f)
                {
                    fired = true;
                    triggerClimax();
                }

                float crouch = 0.34f * env * (1f - lift * 0.8f);
                hum.SetDualHandAction(true, lHand, rHand, env, env, Mathf.Lerp(38f, 4f, lift) * env, Mathf.Lerp(24f, 6f, lift) * env, crouch);
                yield return null;
            }
        }

        private IEnumerator RunMilkCow(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.70f;
            float t = 0f;
            bool fired = false;
            float splashTimer = 0.24f;

            // Place a gleaming stainless-steel milk pail on the grass right beneath Deniz's hands!
            if (_foot != null)
            {
                Vector3 bucketPos = _foot.transform.position + _foot.transform.forward * 0.60f;
                bucketPos.y = _foot.transform.position.y;
                _groundProp = SpawnMilkBucket(bucketPos);
            }

            while (t < total)
            {
                if (ShouldAbort(mode)) yield break;
                float dt = Time.deltaTime;
                t += dt;
                float norm = Mathf.Clamp01(t / total);
                float env = EdgeBlend(norm, 0.18f, 0.82f);

                Transform tr = _foot.transform;
                Vector3 fwd = tr.forward;
                Vector3 right = tr.right;
                Vector3 center = tr.position + fwd * 0.56f + Vector3.up * 0.62f;

                // Rhythmic alternating left-right downward milking strokes!
                float stroke = Mathf.Sin(t * 13f);
                Vector3 lHand = center - right * 0.12f + Vector3.up * (stroke * 0.10f);
                Vector3 rHand = center + right * 0.12f - Vector3.up * (stroke * 0.10f);

                splashTimer -= dt;
                if (splashTimer <= 0f && norm > 0.20f && norm < 0.80f)
                {
                    splashTimer = 0.24f;
                    GameFeelAudio.SpawnBurst(center - Vector3.up * 0.24f, new Color(0.96f, 0.98f, 1.0f), count: 7, spread: 0.22f);
                }

                if (!fired && norm >= 0.55f)
                {
                    fired = true;
                    triggerClimax();
                }

                hum.SetDualHandAction(true, lHand, rHand, env, env, 30f * env, 18f * env, 0.38f * env);
                yield return null;
            }
        }

        private IEnumerator RunShearSheep(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.55f;
            float t = 0f;
            bool fired = false;
            float tuftTimer = 0.24f;

            _heldProp = SpawnActionProp(hum.RightForearm, PlayerActionKind.ShearSheep);

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
                Vector3 back = tr.position + fwd * 0.56f + Vector3.up * 0.72f;

                float sweep = Mathf.Sin(t * 10f);
                Vector3 lHand = back - right * 0.18f;
                Vector3 rHand = back + right * (0.04f + sweep * 0.24f) + fwd * (Mathf.Cos(t * 10f) * 0.10f);
                _heldPropLocalRot = Quaternion.Euler(20f * env, sweep * 18f, Mathf.Sin(t * 18f) * 10f);

                tuftTimer -= dt;
                if (tuftTimer <= 0f && norm > 0.18f && norm < 0.82f)
                {
                    tuftTimer = 0.22f;
                    GameFeelAudio.SpawnBurst(rHand, new Color(0.97f, 0.95f, 0.89f), count: 6, spread: 0.28f);
                }

                if (!fired && norm >= 0.55f)
                {
                    fired = true;
                    triggerClimax();
                }

                hum.SetDualHandAction(true, lHand, rHand, env * 0.85f, env, 34f * env, 22f * env, 0.26f * env);
                yield return null;
            }
        }

        private IEnumerator RunGrillMangal(PlayerModeController mode, ProceduralHumanoid hum, Transform target, Action triggerClimax)
        {
            const float total = 1.55f;
            float t = 0f;
            bool fired = false;
            float sparkTimer = 0.22f;

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
                Vector3 grill = tr.position + fwd * 0.58f + Vector3.up * 0.98f;

                float flip = Mathf.Sin(t * 8.5f);
                Vector3 rHand = grill + right * (0.12f + flip * 0.16f) + Vector3.up * (Mathf.Abs(flip) * 0.10f);
                Vector3 lHand = tr.position + fwd * 0.24f - right * 0.22f + Vector3.up * 0.96f;
                _heldPropLocalRot = Quaternion.Euler(14f * env, flip * 16f, flip * 18f);

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

                hum.SetDualHandAction(true, lHand, rHand, env * 0.5f, env, 18f * env, 16f * env, 0.04f * env);
                yield return null;
            }
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
                case PlayerActionKind.UseTvRemote:
                    return 1.55f;
                case PlayerActionKind.FarmPlantAndDig:
                case PlayerActionKind.FarmWatering:
                case PlayerActionKind.FarmHarvest:
                    return 1.10f;
                case PlayerActionKind.MilkCow:
                case PlayerActionKind.ShearSheep:
                    return 1.25f;
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
            }

            if (_heldProp != null)
            {
                Destroy(_heldProp);
                _heldProp = null;
            }
            if (_groundProp != null)
            {
                Destroy(_groundProp);
                _groundProp = null;
            }
            _heldPropLocalRot = Quaternion.identity;

            if (_holdingModalLock && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.SetModalLock(this, false);
                _holdingModalLock = false;
            }
        }

        private void LateUpdate()
        {
            if (_heldProp == null || _foot == null || _foot.Humanoid == null || _foot.Humanoid.RightForearm == null) return;
            Vector3 palmWorld = _foot.Humanoid.RightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            _heldProp.transform.position = palmWorld + _foot.transform.forward * 0.03f;
            _heldProp.transform.rotation = _foot.transform.rotation * _heldPropLocalRot;
        }

        private static GameObject SpawnMilkBucket(Vector3 groundPos)
        {
            GameObject root = new GameObject("TempMilkBucket");
            root.transform.position = groundPos;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material steel = MakeSimpleMat(urpLit, new Color(0.76f, 0.80f, 0.84f), 0.72f, 0.82f);
            Material milk = MakeSimpleMat(urpLit, new Color(0.98f, 0.98f, 0.96f), 0.55f, 0.0f);
            CreatePropPart("PailBody", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.17f, 0f), new Vector3(0.28f, 0.17f, 0.28f), steel);
            CreatePropPart("MilkTop", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.31f, 0f), new Vector3(0.25f, 0.015f, 0.25f), milk);
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
