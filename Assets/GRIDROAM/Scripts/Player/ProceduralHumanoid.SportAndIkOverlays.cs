using System;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Player
{
    public partial class ProceduralHumanoid
    {
        private void ApplyCarryPetAndSportOverlays(ref FramePose p)
        {
            if (p.petting)
            {
                // Kneel drop is handled cleanly by _kneelBlend; keep spine gently leaned & head warmly tilted toward cat
                p.targetHipsPos.y -= 0.05f * p.petCrouch;
                p.targetHipsPos.z -= 0.03f * p.petCrouch;
                p.targetSpineRot = Vector3.Lerp(p.targetSpineRot, new Vector3(28f, -8f, -4f), p.petCrouch);
                p.targetNeckRot.x = Mathf.Lerp(p.targetNeckRot.x, 12f, p.petCrouch);
                p.targetHeadRot.x = Mathf.Lerp(p.targetHeadRot.x, 14f, p.petCrouch);
                p.targetHeadRot.z += 7f * p.petCrouch;
            }
            if (p.carrying)
            {
                float crouch = p.carry.PickupCrouch;
                float heaviness = p.carry.CarryHeaviness;
                // Squat lift with hips/knees (back stays straighter ~30 deg instead of hunched 53 deg)
                // and counter-lean backward while carrying heavier items.
                float squatDrop = Mathf.Lerp(0.38f, 0.48f, heaviness);
                float liftSpineLean = Mathf.Lerp(32f, 26f, heaviness);
                float carryCounterLean = p.carry.IsAttached ? -Mathf.Lerp(3.5f, 10.5f, heaviness) : 2f;

                p.targetHipsPos.y -= squatDrop * crouch;
                p.targetHipsPos.z -= 0.095f * crouch;
                p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x * 0.45f + carryCounterLean, liftSpineLean, crouch);
                p.targetSpineRot.y *= Mathf.Lerp(0.35f, 0.18f, heaviness);
                p.targetHeadRot.x += Mathf.Lerp(10f, 16f, heaviness) * crouch;

                if (p.carry.IsThrowing)
                {
                    float tp = p.carry.ThrowProgress;
                    float wind = tp < 0.64f
                        ? Mathf.SmoothStep(0f, 1f, tp / 0.64f)
                        : (1f - Mathf.SmoothStep(0f, 1f, (tp - 0.64f) / 0.36f));
                    float heave = tp >= 0.58f ? Mathf.SmoothStep(0f, 1f, (tp - 0.58f) / 0.42f) : 0f;
                    p.targetSpineRot.x += -14f * wind + 20f * heave;
                    p.targetHipsPos.z += -0.05f * wind + 0.07f * heave;
                }
            }
            else if (_carryReleaseTimer >= 0f)
            {
                float relU = Mathf.Clamp01(_carryReleaseTimer / CarryReleaseFadeDuration);
                float relEnv = 1f - Mathf.SmoothStep(0f, 1f, relU);
                p.targetHipsPos.y -= 0.42f * _carryReleaseCrouch * relEnv;
                p.targetHipsPos.z -= 0.08f * _carryReleaseCrouch * relEnv;
                p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, 26f, _carryReleaseCrouch * relEnv);
            }
            else if (_carryThrowTimer >= 0f)
            {
                float fNorm = Mathf.Clamp01(_carryThrowTimer / CarryThrowFollowDuration);
                float fEnv = 1f - Mathf.SmoothStep(0f, 1f, fNorm);
                p.targetSpineRot.x += Mathf.Lerp(14f, 22f, _carryThrowHeaviness) * fEnv;
                p.targetHipsPos.z += 0.05f * fEnv;
                p.targetHipsPos.y -= 0.04f * fEnv;
            }

            // --- Dedicated Full-Body Sports Action Animations (Soccer, Basketball, Volleyball, Celebration) ---
            if (_sportAnimTimer >= 0f && _sportAnimKind != SportAnimKind.None)
            {
                ApplySportAnimOverlay(ref p);
            }

            p.swingTime = Time.time - _tennisContactAt;
            if (p.tennisStance)
            {
                float serveJump = 0f;
                if (_tennisServing && p.swingTime >= -0.22f && p.swingTime <= 0.22f)
                {
                    serveJump = Mathf.Sin(Mathf.InverseLerp(-0.22f, 0.22f, p.swingTime) * Mathf.PI) * 0.24f;
                }
                float readyBounce = Mathf.Abs(Mathf.Sin(_idleTime * 4.2f)) * 0.016f * (1f - p.moveBlend);
                p.targetHipsPos.y += serveJump - 0.072f + readyBounce;
                p.targetHipsRot.y *= 0.35f;
                p.targetSpineRot.x = _tennisServing && p.swingTime < 0.06f ? -14f : 14f;
                float windup = Mathf.Clamp01(1f - Mathf.Abs(p.swingTime + 0.14f) / 0.18f);
                float follow = Mathf.Clamp01(1f - Mathf.Abs(p.swingTime - 0.11f) / 0.20f);
                float sideSign = _tennisBackhand ? -1f : 1f;
                p.targetSpineRot.y += sideSign * (36f * windup - 42f * follow);
                p.targetHipsRot.y += sideSign * (14f * windup - 16f * follow);
                if (serveJump > 0.02f)
                {
                    p.targetLKneeRot.x = Mathf.Lerp(p.targetLKneeRot.x, 26f, serveJump / 0.24f);
                    p.targetRKneeRot.x = Mathf.Lerp(p.targetRKneeRot.x, 30f, serveJump / 0.24f);
                }
            }

            p.doorBodyWeight = 0f;
            if (!p.carrying && !p.petting && !p.tennisStance && !_isLyingDown && !_isSwimming && _doorHandle != null && _grounded)
            {
                float dElapsed = Time.time - _doorReachStarted;
                if (dElapsed < _doorReachDuration)
                {
                    float dIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dElapsed / _doorReachTime));
                    float releaseStart = Mathf.Max(_doorReachTime, _doorReachDuration * 0.64f);
                    float dOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dElapsed - releaseStart) / Mathf.Max(0.08f, _doorReachDuration - releaseStart)));
                    float doorScaleY = transform.lossyScale.y;
                    Vector3 nominalSh = transform.TransformPoint(new Vector3(_doorReachRight ? 0.21f : -0.21f, 1.38f, 0f));
                    float distFade = 1f - Mathf.InverseLerp(0.68f * doorScaleY, 1.18f * doorScaleY, Vector3.Distance(nominalSh, _doorHandle.position));
                    p.doorBodyWeight = dIn * dOut * distFade;
                    p.targetSpineRot.x += 7f * p.doorBodyWeight;
                    p.targetSpineRot.y += (_doorReachRight ? -11f : 11f) * p.doorBodyWeight;
                    p.targetHeadRot.y += (_doorReachRight ? 6f : -6f) * p.doorBodyWeight;
                }
            }

            if (!p.carrying && !p.petting)
            {
                if (_hasShelfReach && _shelfReachWeight > 0.01f)
                {
                    p.targetSpineRot.x += 9f * _shelfReachWeight;
                    p.targetSpineRot.y -= 10f * _shelfReachWeight;
                    p.targetHeadRot.x += 6f * _shelfReachWeight;
                }
                else if (_consumeMode == 1 && _consumeWeight > 0.01f)
                {
                    // Realistic drinking tilt: approach cup gently, tip head back smoothly on sip, subtle swallow bob between sips
                    float sip = Mathf.Clamp01(_consumeCyclePhase);
                    float swallowBob = Mathf.Sin((_idleTime + _noiseSeed) * 8.5f) * 2.2f * (1f - sip) * _consumeWeight;
                    float approachNod = Mathf.Sin(Mathf.Clamp01(sip * 2f) * Mathf.PI) * 4f * _consumeWeight;
                    p.targetHeadRot.x = Mathf.Lerp(p.targetHeadRot.x, -18f, sip * _consumeWeight) + approachNod + swallowBob;
                    p.targetHeadRot.y = Mathf.Lerp(p.targetHeadRot.y, 4f, sip * _consumeWeight * 0.5f);
                    p.targetNeckRot.x = Mathf.Lerp(p.targetNeckRot.x, -7f, sip * _consumeWeight);
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, -3.5f, sip * _consumeWeight);
                }
                else if (_consumeMode == 2 && _consumeWeight > 0.01f)
                {
                    // Realistic eating nod toward snack + subtle chewing micro-motion between bites
                    float bite = Mathf.Clamp01(_consumeCyclePhase);
                    float chew = Mathf.Sin((_idleTime + _noiseSeed) * 19f) * 3.0f * (1f - bite) * _consumeWeight;
                    p.targetHeadRot.x = Mathf.Lerp(p.targetHeadRot.x, 13f, bite * _consumeWeight) + chew;
                    p.targetHeadRot.y = Mathf.Lerp(p.targetHeadRot.y, 6f, bite * _consumeWeight * 0.65f);
                    p.targetHeadRot.z += 3.5f * bite * _consumeWeight;
                    p.targetNeckRot.x = Mathf.Lerp(p.targetNeckRot.x, 5f, bite * _consumeWeight);
                }
                else if (_hasDualAction)
                {
                    float actionBlend = Mathf.Max(_dualLeftWeight, _dualRightWeight);
                    if (actionBlend < 0.01f && (Mathf.Abs(_dualSpineLean) > 0.1f || Mathf.Abs(_dualHeadPitch) > 0.1f || Mathf.Abs(_dualHipsDrop) > 0.001f))
                    {
                        actionBlend = 1f;
                    }
                    p.targetHipsPos.y -= _dualHipsDrop * actionBlend;
                    p.targetSpineRot.x += _dualSpineLean;
                    p.targetNeckRot.x += _dualHeadPitch * 0.35f;
                    p.targetHeadRot.x += _dualHeadPitch * 0.65f;
                }
            }

            // One-knee kneel overlay (farm work, low interactions & cat petting; clamped to hipsBaseY * 0.5f)
            bool canKneel = _grounded && !isSitting && !_isLyingDown && !_isSwimming && _prayerStage == 0 && _knockdownTimer < 0f;
            float desiredKneel = p.petting ? Mathf.Max(_kneelWeight, p.petCrouch * 0.90f) : _kneelWeight;
            _activeKneelRight = p.petting ? true : _kneelRight;
            if (canKneel && (desiredKneel > 0.01f || _kneelBlend > 0.001f))
            {
                float kneelRate = p.petting ? 14f : 8f;
                _kneelBlend = Mathf.Lerp(_kneelBlend, desiredKneel, 1f - Mathf.Exp(-kneelRate * p.dt));
                if (desiredKneel <= 0.01f && _kneelBlend < 0.002f)
                {
                    _kneelBlend = 0f;
                }
                if (_kneelBlend > 0.01f)
                {
                    float kneelDrop = Mathf.Lerp(0f, hipsBaseY * 0.42f, _kneelBlend);
                    p.targetHipsPos.y = Mathf.Max(hipsBaseY - hipsBaseY * 0.5f, p.targetHipsPos.y - kneelDrop);
                    if (!p.petting) p.targetSpineRot.x += 6f * _kneelBlend;
                }
            }
            else if (!canKneel && _kneelBlend > 0f)
            {
                _kneelBlend = Mathf.Lerp(_kneelBlend, 0f, 1f - Mathf.Exp(-10f * p.dt));
                if (_kneelBlend < 0.002f) _kneelBlend = 0f;
            }
        }

        private void ApplySportAnimOverlay(ref FramePose p)
        {
            float sn = Mathf.Clamp01(_sportAnimTimer / Mathf.Max(0.1f, _sportAnimDuration));
            float env = Mathf.Sin(sn * Mathf.PI);

            switch (_sportAnimKind)
            {
                case SportAnimKind.SoccerKick:
                {
                    // Authentic windup -> explosive right-foot strike -> follow-through (continuous curves, zero snap)
                    float strikeBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.24f) / 0.16f));
                    float cock = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sn / 0.26f)) * (1f - strikeBlend);
                    float whip = Mathf.Sin(Mathf.Clamp01((sn - 0.22f) / 0.78f) * Mathf.PI);
                    p.targetHipsPos.y -= 0.06f * env;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, Mathf.Lerp(8f, -16f, strikeBlend), env);
                    p.targetSpineRot.y += 22f * cock - 28f * whip;
                    p.targetSpineRot.z += -12f * whip;
                    // Planted left leg bends naturally, right leg cocks far back then whips through ball
                    p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(-18f, 0f, -6f), env);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(24f, 0f, 0f), env);
                    float kickHip = Mathf.Lerp(46f * cock, -78f, whip);
                    float kneeExtend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.34f) / 0.26f));
                    float kickKnee = Mathf.Lerp(Mathf.Lerp(14f, 76f, cock), Mathf.Lerp(76f, 12f, kneeExtend), strikeBlend);
                    p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(kickHip, 0f, 8f), env);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(kickKnee, 0f, 0f), env);
                    p.targetRAnkleRot = Vector3.Lerp(p.targetRAnkleRot, new Vector3(28f, 0f, 0f), whip);
                    // Counter-balancing arms spread athletically
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-48f, 0f, -42f), env);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-36f, 0f, 0f), env);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(32f, 0f, 34f), env);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-28f, 0f, 0f), env);
                    break;
                }
                case SportAnimKind.SoccerSteal:
                {
                    // Low forward poke-tackle lunge
                    p.targetHipsPos.y -= 0.14f * env;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, 22f, env);
                    p.targetSpineRot.y += -24f * env;
                    p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(-62f, 0f, 10f), env);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(14f, 0f, 0f), env);
                    p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(26f, 0f, -6f), env);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(32f, 0f, 0f), env);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-28f, 0f, -46f), env);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(24f, 0f, 40f), env);
                    break;
                }
                case SportAnimKind.BasketballJumpShot:
                {
                    // Squat -> explosive 0.56 m jump -> high 2-hand overhead shot release & smooth wrist extension
                    float dip = sn < 0.24f ? Mathf.Sin((sn / 0.24f) * Mathf.PI) : 0f;
                    float air = sn >= 0.16f ? Mathf.Sin(Mathf.Clamp01((sn - 0.16f) / 0.84f) * Mathf.PI) : 0f;
                    float riseBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.14f) / 0.16f));
                    float release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.20f) / 0.26f)) *
                                    (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.80f) / 0.20f)));
                    float shootExtend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.34f) / 0.20f));
                    p.targetHipsPos.y += air * 0.56f - dip * 0.14f;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, Mathf.Lerp(14f, -10f, riseBlend), Mathf.Max(dip, air));
                    p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(-18f * air + 16f * dip, 0f, -5f), Mathf.Max(dip, air));
                    p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(-14f * air + 16f * dip, 0f, 5f), Mathf.Max(dip, air));
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(26f * air + 36f * dip, 0f, 0f), Mathf.Max(dip, air));
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(22f * air + 36f * dip, 0f, 0f), Mathf.Max(dip, air));
                    // Right shooting arm extends straight overhead, left guide hand supports cleanly
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-164f, 0f, 12f), release);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(Mathf.Lerp(-54f, -10f, shootExtend), 0f, 0f), release);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-146f, 14f, -12f), release);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(Mathf.Lerp(-44f, -26f, shootExtend), 0f, 0f), release);
                    break;
                }
                case SportAnimKind.BasketballPass:
                {
                    // Snappy two-hand chest pass push with smooth elbow extension
                    float pushExtend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.22f) / 0.24f));
                    p.targetHipsPos.y -= 0.06f * env;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, 14f, env);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-84f, 16f, -8f), env);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-84f, -16f, 8f), env);
                    float passElbow = Mathf.Lerp(-74f, -8f, pushExtend);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(passElbow, 0f, 0f), env);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(passElbow, 0f, 0f), env);
                    break;
                }
                case SportAnimKind.VolleyballBump:
                {
                    // Deep athletic knee crouch -> smooth upward forearm dig platform!
                    float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.25f) / 0.45f));
                    p.targetHipsPos.y -= Mathf.Lerp(0.18f, 0.04f, lift) * env;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, Mathf.Lerp(26f, 10f, lift), env);
                    p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(-28f, 0f, -10f), env);
                    p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(-28f, 0f, 10f), env);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(48f, 0f, 0f), env);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(48f, 0f, 0f), env);
                    float armPitch = Mathf.Lerp(-52f, -94f, lift);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(armPitch, 24f, -4f), env);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(armPitch, -24f, 4f), env);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-6f, 0f, 0f), env);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-6f, 0f, 0f), env);
                    break;
                }
                case SportAnimKind.VolleyballSpike:
                {
                    // Explosive 0.66 m airborne leap -> arched back cock -> smooth overhead spike whip!
                    float air = Mathf.Sin(sn * Mathf.PI);
                    float swingBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.34f) / 0.18f));
                    float cock = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sn / 0.34f)) * (1f - swingBlend);
                    float slam = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.36f) / 0.24f)) *
                                 (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.78f) / 0.22f)));
                    p.targetHipsPos.y += air * 0.66f;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, Mathf.Lerp(-20f, 16f, swingBlend), air);
                    p.targetSpineRot.y += 28f * cock - 34f * slam;
                    p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(-22f, 0f, -5f), air);
                    p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(-18f, 0f, 5f), air);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(58f, 0f, 0f), air);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(64f, 0f, 0f), air);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(Mathf.Lerp(-145f, -40f, swingBlend), 0f, -24f), air);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-18f, 0f, 0f), air);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(Mathf.Lerp(-172f, -48f, slam), -14f, 22f), air);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(Mathf.Lerp(-78f, -12f, swingBlend), 0f, 0f), air);
                    break;
                }
                case SportAnimKind.VictoryJump:
                {
                    // Joyful celebration hop with both fists pumped skyward!
                    float hop = Mathf.Sin(sn * Mathf.PI);
                    p.targetHipsPos.y += hop * 0.40f;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, -10f, hop);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(38f, 0f, 0f), hop);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(38f, 0f, 0f), hop);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-156f, 0f, -34f), hop);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-156f, 0f, 34f), hop);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-26f, 0f, 0f), hop);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-26f, 0f, 0f), hop);
                    break;
                }
                case SportAnimKind.SoccerSlideTackle:
                {
                    // Low turf-skimming slide tackle: hips drop low, lead leg spears straight forward, rear knee tucks!
                    float slide = Mathf.Sin(Mathf.Clamp01(sn / 0.85f) * Mathf.PI);
                    p.targetHipsPos.y -= 0.54f * slide;
                    p.targetHipsRot.x = Mathf.Lerp(p.targetHipsRot.x, -42f, slide);
                    p.targetHipsRot.z = Mathf.Lerp(p.targetHipsRot.z, -24f * _sportAnimSide, slide);
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, 18f, slide);
                    p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(-72f, 0f, 8f), slide);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(8f, 0f, 0f), slide);
                    p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(-28f, -20f, -18f), slide);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(92f, 0f, 0f), slide);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(24f, 0f, -54f), slide);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-36f, 0f, 44f), slide);
                    break;
                }
                case SportAnimKind.SoccerFeint:
                {
                    // Crisp step-over body feint: quick shoulder drop to one side then explosive push opposite!
                    float sway = Mathf.Sin(sn * TwoPi) * _sportAnimSide;
                    p.targetHipsPos.y -= 0.14f * env;
                    p.targetHipsPos.x += sway * 0.16f;
                    p.targetSpineRot.z += -24f * sway;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, 18f, env);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-30f, 0f, -38f), env);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-30f, 0f, 38f), env);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(36f, 0f, 0f), env);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(36f, 0f, 0f), env);
                    break;
                }
                case SportAnimKind.GoalkeeperDive:
                {
                    // Full horizontal diving stretch toward shot corner (sideSign = +1 right, -1 left)
                    float dive = Mathf.Sin(sn * Mathf.PI);
                    float s = _sportAnimSide;
                    p.targetHipsPos.y += (0.20f * Mathf.Sin(Mathf.Clamp01(sn * 1.4f) * Mathf.PI) - 0.34f * Mathf.Clamp01(sn * 1.15f)) * dive;
                    p.targetHipsPos.x += s * 0.52f * dive;
                    p.targetHipsRot.z = Mathf.Lerp(p.targetHipsRot.z, -82f * s, dive);
                    p.targetSpineRot.z = Mathf.Lerp(p.targetSpineRot.z, -10f * s, dive);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-164f, 0f, -14f), dive);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-164f, 0f, 14f), dive);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-8f, 0f, 0f), dive);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-8f, 0f, 0f), dive);
                    p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(14f, 0f, -12f), dive);
                    p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(14f, 0f, 12f), dive);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(24f, 0f, 0f), dive);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(24f, 0f, 0f), dive);
                    break;
                }
                case SportAnimKind.VolleyballBlock:
                {
                    // Vertical 0.72 m roof-block leap with both hands forming a solid net wall overhead!
                    float air = Mathf.Sin(sn * Mathf.PI);
                    p.targetHipsPos.y += air * 0.72f;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, 8f, air);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-172f, 8f, -6f), air);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-172f, -8f, 6f), air);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-4f, 0f, 0f), air);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-4f, 0f, 0f), air);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(26f, 0f, 0f), air);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(26f, 0f, 0f), air);
                    break;
                }
                case SportAnimKind.VolleyballDiveSave:
                {
                    // Athletic forward sand pancake dive: body stretches horizontally near ground, arms extended forward!
                    float dive = Mathf.Sin(sn * Mathf.PI);
                    p.targetHipsPos.y -= 0.56f * dive;
                    p.targetHipsPos.z += 0.34f * dive;
                    p.targetHipsRot.x = Mathf.Lerp(p.targetHipsRot.x, 74f, dive);
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, -18f, dive);
                    p.targetHeadRot.x = Mathf.Lerp(p.targetHeadRot.x, -38f, dive);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-164f, 14f, -8f), dive);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-164f, -14f, 8f), dive);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-6f, 0f, 0f), dive);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-6f, 0f, 0f), dive);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(36f, 0f, 0f), dive);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(44f, 0f, 0f), dive);
                    break;
                }
                case SportAnimKind.VolleyballTip:
                {
                    // Nimble jump + delicate fingertip dink/plase push right over the opponent's block!
                    float air = Mathf.Sin(sn * Mathf.PI);
                    p.targetHipsPos.y += air * 0.44f;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, -6f, air);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-148f, -10f, 14f), air);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-22f, 0f, 0f), air);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-76f, 0f, -24f), air);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(34f, 0f, 0f), air);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(40f, 0f, 0f), air);
                    break;
                }
                case SportAnimKind.SwimDiveStart:
                {
                    // Olympic streamlined arrow dive: holds crisp head-first entry through water splash before leveling into stroke
                    float entryIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sn / 0.22f));
                    float entryOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sn - 0.84f) / 0.16f));
                    float diveW = entryIn * entryOut;
                    float pitchT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sn / 0.65f));
                    float arc = Mathf.Sin(sn * Mathf.PI);
                    p.targetHipsRot.x = Mathf.Lerp(p.targetHipsRot.x, Mathf.Lerp(28f, 96f, pitchT), diveW);
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, 12f, diveW);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-176f, 0f, -6f), diveW);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-176f, 0f, 6f), diveW);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, Vector3.zero, diveW);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, Vector3.zero, diveW);
                    p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(12f * arc, 0f, -4f), diveW);
                    p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(12f * arc, 0f, 4f), diveW);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(6f, 0f, 0f), diveW);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(6f, 0f, 0f), diveW);
                    p.targetLAnkleRot = Vector3.Lerp(p.targetLAnkleRot, new Vector3(42f, 0f, 0f), diveW);
                    p.targetRAnkleRot = Vector3.Lerp(p.targetRAnkleRot, new Vector3(42f, 0f, 0f), diveW);
                    break;
                }
                case SportAnimKind.SwimFlipTurn:
                {
                    // Tucked 360-degree Olympic flip turn / mid-air diving somersault!
                    float tuck = Mathf.Sin(sn * Mathf.PI);
                    p.targetHipsRot.x = sn * 360f;
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, 42f, tuck);
                    p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(-92f, 0f, -8f), tuck);
                    p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(-92f, 0f, 8f), tuck);
                    p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(115f, 0f, 0f), tuck);
                    p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(115f, 0f, 0f), tuck);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-68f, 0f, -24f), tuck);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-68f, 0f, 24f), tuck);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-86f, 0f, 0f), tuck);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-86f, 0f, 0f), tuck);
                    break;
                }
            }
        }

        private void ApplyCoreBoneTransformsAndFootPlanting(ref FramePose p)
        {
            if (chestVisual != null)
            {
                float breathScale = 1f + p.breathWave * 0.018f * (1f - p.moveBlend);
                Vector3 targetScale = new Vector3(
                    _chestBaseScale.x * breathScale,
                    _chestBaseScale.y,
                    _chestBaseScale.z * breathScale);
                chestVisual.localScale = Vector3.Lerp(chestVisual.localScale, targetScale, 1f - Mathf.Exp(-6f * p.dt));
            }

            float damp = _isLyingDown ? 5.5f : (_prayerStage > 0 ? 6.5f : (_landSquatTimer >= 0f || p.carrying || p.petting || _carryThrowTimer >= 0f ? 25f : (_grounded ? 14f : 10f)));
            _baseHipsLocalPos = Vector3.Lerp(_baseHipsLocalPos, p.targetHipsPos, 1f - Mathf.Exp(-damp * p.dt));
            hips.localPosition = _baseHipsLocalPos;
            SmoothRotate(hips, p.targetHipsRot, damp, p.dt);
            SmoothRotate(spine, p.targetSpineRot, damp, p.dt);
            SmoothRotate(neck, p.targetNeckRot, damp, p.dt);
            SmoothRotate(head, p.targetHeadRot, _isLyingDown ? 4.5f : (_prayerStage > 0 ? 6.0f : 9f), p.dt);

            SmoothRotate(lHip, p.targetLHipRot, damp, p.dt);
            SmoothRotate(rHip, p.targetRHipRot, damp, p.dt);
            SmoothRotate(lKnee, p.targetLKneeRot, damp, p.dt);
            SmoothRotate(rKnee, p.targetRKneeRot, damp, p.dt);
            SmoothRotate(lAnkle, p.targetLAnkleRot, damp, p.dt);
            SmoothRotate(rAnkle, p.targetRAnkleRot, damp, p.dt);

            SmoothRotate(lShoulder, p.targetLShoulderRot, damp, p.dt);
            SmoothRotate(rShoulder, p.targetRShoulderRot, damp, p.dt);
            SmoothRotate(lElbow, p.targetLElbowRot, damp, p.dt);
            SmoothRotate(rElbow, p.targetRElbowRot, damp, p.dt);

            p.leftGrip = Vector3.zero;
            p.rightGrip = Vector3.zero;
            p.carryWeight = 0f;
            if (p.carrying)
            {
                p.carryWeight = p.carry.UpdateGrip(this, hips.position, out p.leftGrip, out p.rightGrip);
                if (p.carryWeight > 0f)
                {
                    Vector3 leftReach = ShoulderReach(lShoulder, lElbow, p.leftGrip);
                    Vector3 rightReach = ShoulderReach(rShoulder, rElbow, p.rightGrip);
                    Vector3 reach = leftReach.sqrMagnitude > rightReach.sqrMagnitude ? leftReach : rightReach;
                    hips.position += Vector3.ClampMagnitude(reach, 0.16f * transform.lossyScale.y) * p.carryWeight;
                }
            }
            if (p.petting)
            {
                Vector3 reach = ShoulderReach(rShoulder, rElbow, p.petHand);
                hips.position += Vector3.ClampMagnitude(reach, 0.13f * transform.lossyScale.y) * p.petWeight;
            }
            else if (p.doorBodyWeight > 0.01f && _doorHandle != null)
            {
                Transform dSh = _doorReachRight ? rShoulder : lShoulder;
                Transform dElb = _doorReachRight ? rElbow : lElbow;
                Vector3 dReach = ShoulderReach(dSh, dElb, _doorHandle.position);
                hips.position += Vector3.ClampMagnitude(dReach, 0.08f * transform.lossyScale.y) * p.doorBodyWeight;
            }

            bool sportAirOrKick = _sportAnimTimer >= 0f &&
                                  (_sportAnimKind == SportAnimKind.SoccerKick ||
                                   _sportAnimKind == SportAnimKind.SoccerSteal ||
                                   _sportAnimKind == SportAnimKind.SoccerSlideTackle ||
                                   _sportAnimKind == SportAnimKind.SoccerFeint ||
                                   _sportAnimKind == SportAnimKind.GoalkeeperDive ||
                                   _sportAnimKind == SportAnimKind.BasketballJumpShot ||
                                   _sportAnimKind == SportAnimKind.VolleyballSpike ||
                                   _sportAnimKind == SportAnimKind.VolleyballBlock ||
                                   _sportAnimKind == SportAnimKind.VolleyballDiveSave ||
                                   _sportAnimKind == SportAnimKind.VolleyballTip ||
                                   _sportAnimKind == SportAnimKind.SwimDiveStart ||
                                   _sportAnimKind == SportAnimKind.SwimFlipTurn ||
                                   _sportAnimKind == SportAnimKind.VictoryJump);
            bool tennisJumpServe = p.tennisStance && _tennisServing && p.swingTime >= -0.22f && p.swingTime <= 0.22f;

            float footPlantWeight = _knockdownTimer >= 0f ? p.knockdownFootBlend : 1f;
            if (_grounded && !_isSwimming && !_isLyingDown && _prayerStage == 0 && _vaultTimer < 0f &&
                _punchTimer < 0f && footPlantWeight > 0.005f &&
                !sportAirOrKick && !tennisJumpServe &&
                lAnkle != null && rAnkle != null)
            {
                bool stationaryGround = _speedMs < 0.015f && p.absYawRate < 0.25f && _turnInPlaceWeight < 0.01f;
                PlantFeet(p.strideLength, p.stance, p.runBlend, p.tennisStance, p.idleW, p.localTravel, p.dt,
                    footPlantWeight, p.startledStepW, p.hitStaggerW, stationaryGround);
            }
            else
            {
                _smoothedPelvisDrop = Mathf.Lerp(_smoothedPelvisDrop, 0f, 1f - Mathf.Exp(-10f * p.dt));
            }
        }

        private void ApplyPostTransformHandIk(ref FramePose p)
        {
            ApplyPrayerHandIk(ref p);
            ApplyCarryPetVaultSwimDoorIk(ref p);
            ApplyWeaponAimPoseIk(ref p);
        }

        private void ApplyPrayerHandIk(ref FramePose p)
        {
            // Smoothly blended 2-bone IK hand placements for every Prayer (Namaz) stage so hands glide naturally between ears, abdomen, knees, carpet & thighs!
            if (_prayerStage > 0 && _prayerStage != 6 && !_isLyingDown && lShoulder != null && lElbow != null && rShoulder != null && rElbow != null)
            {
                Vector3 leftWorld = Vector3.zero;
                Vector3 rightWorld = Vector3.zero;
                Vector3 leftPole = -transform.right;
                Vector3 rightPole = transform.right;
                bool hasTargets = false;

                if (_prayerStage == 5 && head != null)
                {
                    // İftitah Tekbiri: both hands raised respectfully beside the ears
                    leftWorld = head.position - transform.right * 0.21f + transform.forward * 0.09f + transform.up * 0.04f;
                    rightWorld = head.position + transform.right * 0.21f + transform.forward * 0.09f + transform.up * 0.04f;
                    leftPole = -transform.right - transform.up * 0.5f;
                    rightPole = transform.right - transform.up * 0.5f;
                    hasTargets = true;
                }
                else if (_prayerStage == 1 && spine != null)
                {
                    // Kıyam: right hand folded cleanly over left hand at navel/abdomen
                    leftWorld = spine.position + transform.forward * 0.165f + transform.up * 0.075f - transform.right * 0.01f;
                    rightWorld = spine.position + transform.forward * 0.185f + transform.up * 0.085f + transform.right * 0.01f;
                    leftPole = -transform.right - transform.up * 0.35f;
                    rightPole = transform.right - transform.up * 0.35f;
                    hasTargets = true;
                }
                else if (_prayerStage == 2 && lKnee != null && rKnee != null)
                {
                    // Rükû: both palms directly grasping the knees
                    leftWorld = lKnee.position + transform.forward * 0.04f + transform.up * 0.02f;
                    rightWorld = rKnee.position + transform.forward * 0.04f + transform.up * 0.02f;
                    hasTargets = true;
                }
                else if (_prayerStage == 3)
                {
                    // Secde: both palms placed flat on the carpet right beside the head, elbows lifted outward
                    leftWorld = transform.TransformPoint(new Vector3(-0.23f, 0.045f, 0.36f));
                    rightWorld = transform.TransformPoint(new Vector3(0.23f, 0.045f, 0.36f));
                    leftPole = -transform.right + transform.up * 0.45f;
                    rightPole = transform.right + transform.up * 0.45f;
                    hasTargets = true;
                }
                else if (_prayerStage == 4 && lKnee != null && rKnee != null)
                {
                    // Oturuş / Tahiyyat & Selam: both palms resting cleanly on thighs just behind knees
                    leftWorld = lKnee.position - transform.forward * 0.09f + transform.up * 0.085f;
                    rightWorld = rKnee.position - transform.forward * 0.09f + transform.up * 0.085f;
                    hasTargets = true;
                }

                if (hasTargets)
                {
                    Vector3 targetL = transform.InverseTransformPoint(leftWorld);
                    Vector3 targetR = transform.InverseTransformPoint(rightWorld);
                    if (!_prayerHandsInit)
                    {
                        _prayerLeftHandLocal = targetL;
                        _prayerRightHandLocal = targetR;
                        _prayerHandsInit = true;
                    }
                    else
                    {
                        float hBlend = 1f - Mathf.Exp(-8.5f * p.dt);
                        _prayerLeftHandLocal = Vector3.Lerp(_prayerLeftHandLocal, targetL, hBlend);
                        _prayerRightHandLocal = Vector3.Lerp(_prayerRightHandLocal, targetR, hBlend);
                    }
                    _prayerHandFade = Mathf.MoveTowards(_prayerHandFade, 1f, p.dt * 6f);
                    AimHand(lShoulder, lElbow, transform.TransformPoint(_prayerLeftHandLocal), leftPole, _prayerHandFade);
                    AimHand(rShoulder, rElbow, transform.TransformPoint(_prayerRightHandLocal), rightPole, _prayerHandFade);
                }
            }
            else if (_prayerHandFade > 0.005f && _prayerHandsInit && !_isLyingDown && lShoulder != null && lElbow != null && rShoulder != null && rElbow != null)
            {
                _prayerHandFade = Mathf.MoveTowards(_prayerHandFade, 0f, p.dt * 4.2f);
                if (_prayerHandFade > 0.005f)
                {
                    AimHand(lShoulder, lElbow, transform.TransformPoint(_prayerLeftHandLocal), -transform.right, _prayerHandFade);
                    AimHand(rShoulder, rElbow, transform.TransformPoint(_prayerRightHandLocal), transform.right, _prayerHandFade);
                }
                else
                {
                    _prayerHandsInit = false;
                }
            }
        }

        private void ApplyCarryPetVaultSwimDoorIk(ref FramePose p)
        {
            if (p.carryWeight > 0f)
            {
                AimHand(lShoulder, lElbow, p.leftGrip, -transform.right, p.carryWeight);
                AimHand(rShoulder, rElbow, p.rightGrip, transform.right, p.carryWeight);
            }
            else if (_carryReleaseTimer >= 0f)
            {
                float relW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_carryReleaseTimer / CarryReleaseFadeDuration));
                if (relW > 0.005f)
                {
                    AimHand(lShoulder, lElbow, _carryReleaseLeft, -transform.right, relW);
                    AimHand(rShoulder, rElbow, _carryReleaseRight, transform.right, relW);
                }
            }
            else if (_carryThrowTimer >= 0f)
            {
                float fNorm = Mathf.Clamp01(_carryThrowTimer / CarryThrowFollowDuration);
                float arc = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fNorm / 0.35f));
                float fW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fNorm - 0.14f) / 0.86f));
                if (fW > 0.005f)
                {
                    Vector3 followOff = new Vector3(0f, 0.08f * arc, 0.15f * arc);
                    AimHand(lShoulder, lElbow, transform.TransformPoint(_carryThrowLeftLocal + followOff), -transform.right, fW);
                    AimHand(rShoulder, rElbow, transform.TransformPoint(_carryThrowRightLocal + followOff), transform.right, fW);
                }
            }
            if (p.petting)
            {
                AimHand(rShoulder, rElbow, p.petHand, transform.right, p.petWeight);
                if (lKnee != null)
                    AimHand(lShoulder, lElbow, lKnee.position + transform.up * 0.09f - transform.right * 0.03f,
                        -transform.right, p.petWeight);
            }
            else if (_petFadeBlend > 0.005f)
            {
                AimHand(rShoulder, rElbow, _lastPetHandWorld, transform.right, _petFadeBlend);
                if (lKnee != null)
                    AimHand(lShoulder, lElbow, lKnee.position + transform.up * 0.09f - transform.right * 0.03f,
                        -transform.right, _petFadeBlend);
            }
            if (_vaultTimer >= 0f && _vaultSupport.HasValue && rShoulder != null)
            {
                Vector3 palm = _vaultSupport.Value + transform.right * 0.12f;
                float time = _vaultTimer / VaultDuration;
                float plant = Mathf.SmoothStep(0f, 1f, (time - 0.13f) / 0.15f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (time - 0.50f) / 0.18f));
                float reachable = 1f - Mathf.InverseLerp(0.51f, 0.74f, Vector3.Distance(rShoulder.position, palm));
                AimHand(rShoulder, rElbow, palm, transform.right, plant * reachable);
            }
            if (_isSwimming && !_swimBreaststroke)
            {
                float strokeBlend = Mathf.SmoothStep(0f, 1f, _swimSpeed / 1.70f);
                AimHand(lShoulder, lElbow, SwimHand(-1f, _swimPhase), -transform.right, strokeBlend);
                AimHand(rShoulder, rElbow, SwimHand(1f, _swimPhase + Mathf.PI), transform.right, strokeBlend);
            }
            else if (!p.carrying && !p.petting && !p.tennisStance && !_isLyingDown && _doorHandle != null && _grounded)
            {
                float elapsed = Time.time - _doorReachStarted;
                Transform reachSh = _doorReachRight ? rShoulder : lShoulder;
                Transform reachElb = _doorReachRight ? rElbow : lElbow;
                float doorScaleY = transform.lossyScale.y;
                float reachFade = reachSh != null
                    ? (1f - Mathf.InverseLerp(0.68f * doorScaleY, 1.18f * doorScaleY, Vector3.Distance(reachSh.position, _doorHandle.position)))
                    : 1f;
                float weight = p.doorBodyWeight * reachFade;
                if (weight > 0.005f)
                    AimHand(reachSh, reachElb, _doorHandle.position, transform.right * (_doorReachRight ? 1f : -1f), weight);
                if (elapsed >= _doorReachDuration) _doorHandle = null;
            }

            if (p.tennisStance) ApplyTennisRacketPose(p.swingTime);

            // Shop shelf IK reach & drink/eat mouth-target IK overlay
            if (!p.carrying && !p.petting && _carryReleaseTimer < 0f && _carryThrowTimer < 0f &&
                !p.tennisStance && !_isSwimming && !_isLyingDown && rShoulder != null && rElbow != null)
            {
                if (_hasShelfReach && _shelfReachWeight > 0.01f)
                {
                    AimHand(rShoulder, rElbow, _shelfReachWorldPos, transform.right, _shelfReachWeight);
                }
                else if (_consumeMode > 0 && _consumeWeight > 0.01f && head != null)
                {
                    float phase = Mathf.Clamp01(_consumeCyclePhase);
                    Vector3 holdPos = head.position + transform.forward * 0.28f + transform.right * 0.14f - transform.up * 0.24f;
                    Vector3 mouthPos = _consumeMode == 1
                        ? head.position + transform.forward * 0.17f + transform.right * 0.03f - transform.up * 0.01f
                        : head.position + transform.forward * 0.19f + transform.right * 0.04f - transform.up * 0.06f;
                    Vector3 handTarget = Vector3.Lerp(holdPos, mouthPos, phase);
                    AimHand(rShoulder, rElbow, handTarget, transform.right, _consumeWeight);
                }
            }

            // Dual-hand physical interaction IK overlay (washing face/hands, abdest, typing, ATM, remote, etc.)
            if (!p.carrying && !p.petting && _carryReleaseTimer < 0f && _carryThrowTimer < 0f &&
                !p.tennisStance && !_isSwimming && !_isLyingDown && _hasDualAction)
            {
                if (_dualLeftWeight > 0.01f && lShoulder != null && lElbow != null)
                {
                    AimHand(lShoulder, lElbow, _dualLeftTarget, -transform.right, _dualLeftWeight);
                }
                if (_dualRightWeight > 0.01f && rShoulder != null && rElbow != null)
                {
                    AimHand(rShoulder, rElbow, _dualRightTarget, transform.right, _dualRightWeight);
                }
            }
        }

        private void ApplyWeaponAimPoseIk(ref FramePose p)
        {
            // Shooting Range two-handed sporting firearm stance (smooth raise/lower, weapon switch blend, pump/bolt/reload hand sync)
            float weaponAimTarget = _weaponAimActive ? 1f : 0f;
            _weaponAimBlend = Mathf.Lerp(_weaponAimBlend, weaponAimTarget, 1f - Mathf.Exp(-14f * p.dt));
            if (_weaponAimBlend < 0.004f && !_weaponAimActive) _weaponAimBlend = 0f;
            _weaponLongGunBlend = Mathf.Lerp(_weaponLongGunBlend, _weaponIsLongGun ? 1f : 0f, 1f - Mathf.Exp(-11f * p.dt));

            if (_weaponAimBlend > 0.005f && rShoulder != null && rElbow != null && lShoulder != null && lElbow != null)
            {
                float longW = _weaponLongGunBlend;
                float reloadDip = _weaponReloadPhase > 0.005f ? Mathf.Sin(_weaponReloadPhase * Mathf.PI) : 0f;
                float kickPitch = _weaponRecoilKick * Mathf.Lerp(14.0f, 9.5f, longW);
                float raiseDrop = (1f - _weaponAimBlend) * 24f;
                Quaternion aimRot = transform.rotation * Quaternion.Euler(
                    _weaponAimPitchDeg - kickPitch + reloadDip * 18f + raiseDrop,
                    0f,
                    reloadDip * -14f);
                Vector3 aimFwd = aimRot * Vector3.forward;
                Vector3 aimRight = aimRot * Vector3.right;
                Vector3 aimUp = aimRot * Vector3.up;

                if (spine != null)
                {
                    Quaternion aimSpine = Quaternion.Euler(
                        4f - _weaponRecoilKick * 3.5f,
                        Mathf.Lerp(-6f, -14f, longW),
                        0f);
                    spine.localRotation = Quaternion.Slerp(spine.localRotation, aimSpine, _weaponAimBlend);
                }
                if (head != null)
                {
                    Quaternion aimHead = Quaternion.Euler(
                        Mathf.Clamp(_weaponAimPitchDeg * 0.55f, -24f, 24f),
                        Mathf.Lerp(6f, 14f, longW),
                        Mathf.Lerp(0f, 5f, longW));
                    head.localRotation = Quaternion.Slerp(head.localRotation, aimHead, _weaponAimBlend);
                }

                Vector3 rPistol = rShoulder.position
                    + aimFwd * (0.42f - _weaponRecoilKick * 0.055f)
                    - aimRight * 0.11f
                    + aimUp * (0.045f + _weaponRecoilKick * 0.025f);
                Vector3 rLong = rShoulder.position
                    + aimFwd * (0.27f - _weaponRecoilKick * 0.050f)
                    - aimRight * 0.03f
                    - aimUp * (0.015f - _weaponRecoilKick * 0.018f);
                Vector3 rBase = Vector3.Lerp(rPistol, rLong, longW);
                rBase -= aimUp * (reloadDip * 0.12f + (1f - _weaponAimBlend) * 0.16f);

                Vector3 lPistol = rBase - aimFwd * 0.018f - aimRight * 0.028f - aimUp * 0.025f;
                Vector3 lLong = rBase + aimFwd * 0.31f - aimRight * 0.025f + aimUp * 0.015f;
                Vector3 lTarget = Vector3.Lerp(lPistol, lLong, longW);
                Vector3 rHandTarget = rBase;

                if (_weaponReloadPhase > 0.02f)
                {
                    if (_weaponReloadStyle == 1)
                    {
                        // Shotgun shell-by-shell feed: 3 rhythmic dips from hip shell pouch into underside loading port
                        float shellPhase = Mathf.Repeat(_weaponReloadPhase * 3f, 1f);
                        float shellDip = Mathf.Sin(shellPhase * Mathf.PI);
                        lTarget = rBase + aimFwd * 0.14f - aimUp * (0.045f + shellDip * 0.28f) - aimRight * (0.03f + shellDip * 0.10f);
                    }
                    else if (_weaponReloadStyle == 2)
                    {
                        // Sniper reload: 0..0.72 mag swap with left hand, 0.72..1.0 right hand racks bolt cleanly
                        if (_weaponReloadPhase < 0.72f)
                        {
                            float magNorm = _weaponReloadPhase / 0.72f;
                            float pouchPull = Mathf.Sin(Mathf.Clamp01(magNorm * 1.35f) * Mathf.PI);
                            lTarget = rBase + aimFwd * 0.14f - aimUp * (0.06f + pouchPull * 0.32f) - aimRight * (0.04f + pouchPull * 0.08f);
                        }
                        else
                        {
                            float boltCycle = Mathf.Clamp01((_weaponReloadPhase - 0.72f) / 0.28f);
                            float liftW = Mathf.Sin(boltCycle * Mathf.PI);
                            float pullBack = Mathf.Sin(Mathf.Clamp01((boltCycle - 0.15f) / 0.70f) * Mathf.PI);
                            rHandTarget = rBase + aimUp * (0.065f * liftW) + aimRight * (0.06f * liftW) + aimFwd * (0.055f - 0.075f * pullBack);
                        }
                    }
                    else
                    {
                        // Pistol / Carbine mag swap: left hand drops to belt pouch then seats magazine into magwell
                        float pouchPull = Mathf.Sin(Mathf.Clamp01(_weaponReloadPhase * 1.45f) * Mathf.PI);
                        lTarget = rBase + aimFwd * Mathf.Lerp(0.01f, 0.12f, longW)
                            - aimUp * (0.06f + pouchPull * 0.34f)
                            - aimRight * (0.04f + pouchPull * 0.08f);
                    }
                }
                else if (_weaponActionKind == 1 && _weaponActionPhase > 0.005f)
                {
                    // Shotgun pump cycle: left hand tracks forend pump slider back and forward cleanly
                    float pumpBack = Mathf.Sin(_weaponActionPhase * Mathf.PI);
                    lTarget -= aimFwd * (pumpBack * 0.095f);
                }
                else if (_weaponActionKind == 2 && _weaponActionPhase > 0.005f)
                {
                    // Sniper bolt cycle: right hand lifts off grip to bolt handle, pulls back, pushes forward & locks
                    float liftW = Mathf.Sin(_weaponActionPhase * Mathf.PI);
                    float pullBack = Mathf.Sin(Mathf.Clamp01((_weaponActionPhase - 0.15f) / 0.70f) * Mathf.PI);
                    rHandTarget = rBase + aimUp * (0.065f * liftW) + aimRight * (0.06f * liftW) + aimFwd * (0.055f - 0.075f * pullBack);
                }

                AimHand(rShoulder, rElbow, rHandTarget, transform.right * 0.75f - Vector3.up * 0.55f, _weaponAimBlend);
                AimHand(lShoulder, lElbow, lTarget, -transform.right * 0.75f - Vector3.up * 0.55f, _weaponAimBlend);

                WeaponGripWorldPos = rBase;
                WeaponGripWorldRot = aimRot;
            }
        }
    }
}
