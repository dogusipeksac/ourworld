using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace OurWorld.Player
{
    public partial class PlayerActionAnimator
    {
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

    }
}
