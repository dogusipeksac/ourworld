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
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
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

    }
}
