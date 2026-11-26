using System;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Player
{
    public partial class ProceduralHumanoid
    {
        private struct FramePose
        {
            public float dt;
            public CarryableProp carry;
            public bool carrying;
            public Vector3 petHand;
            public float petCrouch;
            public float petWeight;
            public StreetCatController cat;
            public bool petting;
            public float moveBlend;
            public float runBlend;
            public Vector3 localTravel;
            public float fwdDot;
            public bool tennisStance;
            public float stance;
            public float strideLength;
            public float absYawRate;
            public float breathWave;
            public float idleChestPitch;
            public Vector3 targetHipsPos;
            public Vector3 targetHipsRot;
            public Vector3 targetSpineRot;
            public Vector3 targetNeckRot;
            public Vector3 targetHeadRot;
            public Vector3 targetLHipRot;
            public Vector3 targetRHipRot;
            public Vector3 targetLKneeRot;
            public Vector3 targetRKneeRot;
            public Vector3 targetLAnkleRot;
            public Vector3 targetRAnkleRot;
            public Vector3 targetLShoulderRot;
            public Vector3 targetRShoulderRot;
            public Vector3 targetLElbowRot;
            public Vector3 targetRElbowRot;
            public float idleW;
            public float knockdownFootBlend;
            public float startledStepW;
            public float hitStaggerW;
            public float swingTime;
            public float doorBodyWeight;
            public Vector3 leftGrip;
            public Vector3 rightGrip;
            public float carryWeight;
        }

        private void TickActionTimers(float dt)
        {
            _idleTime += dt;
            if (_isLyingDown)
            {
                _lyingBlend = Mathf.MoveTowards(_lyingBlend, _lyingTarget ? 1f : 0f, dt / Mathf.Max(0.05f, _lyingTransition));
                if (!_lyingTarget && _lyingBlend <= 0f)
                {
                    _isLyingDown = false;
                    _lyingStirTimer = -1f;
                }
                else if (_lyingBlend >= 0.999f && _lyingStirTimer < 0f)
                {
                    _lyingStirCountdown -= dt;
                    if (_lyingStirCountdown <= 0f)
                    {
                        _lyingStirTimer = 0f;
                        _lyingStirSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                        _lyingStirCountdown = UnityEngine.Random.Range(9f, 20f);
                    }
                }
            }
            if (!_grounded)
            {
                _airborneTimer += dt;
            }

            if (_landSquatTimer >= 0f)
            {
                _landSquatTimer += dt;
                if (_landSquatTimer >= _landSquatDuration)
                {
                    _landSquatTimer = -1f;
                }
            }

            if (_carryReleaseTimer >= 0f)
            {
                _carryReleaseTimer += dt;
                if (_carryReleaseTimer >= CarryReleaseFadeDuration)
                {
                    _carryReleaseTimer = -1f;
                }
            }

            if (_carryThrowTimer >= 0f)
            {
                _carryThrowTimer += dt;
                if (_carryThrowTimer >= CarryThrowFollowDuration)
                {
                    _carryThrowTimer = -1f;
                }
            }

            if ((transform.position - _prevWorldPos).sqrMagnitude > 1.0f)
            {
                _leftGroundYOffset = 0f;
                _rightGroundYOffset = 0f;
                _leftRawGroundYOffset = 0f;
                _rightRawGroundYOffset = 0f;
                _leftGroundNormal = transform.up;
                _rightGroundNormal = transform.up;
                _leftRawGroundNormal = transform.up;
                _rightRawGroundNormal = transform.up;
                _smoothedPelvisDrop = 0f;
                _groundSampleValid = false;
            }
            _prevWorldPos = transform.position;

            if (_startledTimer >= 0f)
            {
                _startledTimer += dt;
                if (_startledTimer >= _startledDuration)
                {
                    _startledTimer = -1f;
                }
            }

            if (autoRandomWave && !isSitting && !_isLyingDown && _speedMs < 0.1f)
            {
                _autoWaveCountdown -= dt;
                if (_autoWaveCountdown <= 0f)
                {
                    PlayWave();
                    _autoWaveCountdown = UnityEngine.Random.Range(8f, 12f);
                }
            }

            if (_waveTimer >= 0f)
            {
                _waveTimer += dt;
                if (_waveTimer >= WaveDuration)
                {
                    _waveTimer = -1f;
                }
            }

            if (_punchTimer >= 0f)
            {
                _punchTimer += dt;
                float dur = _combatMoveIndex == 2 ? KickDuration : PunchDuration;
                if (_punchTimer >= dur)
                {
                    _punchTimer = -1f;
                }
            }

            if (_hitReactTimer >= 0f)
            {
                _hitReactTimer += dt;
                if (_hitReactTimer >= HitReactDuration)
                {
                    _hitReactTimer = -1f;
                }
            }

            if (_knockdownTimer >= 0f)
            {
                _knockdownTimer += dt;
                if (_knockdownTimer >= _activeKnockdownDuration)
                {
                    _knockdownTimer = -1f;
                }
            }

            if (_vaultTimer >= 0f)
            {
                _vaultTimer += dt;
                if (_vaultTimer >= VaultDuration)
                {
                    _vaultTimer = -1f;
                }
            }

            if (_sportAnimTimer >= 0f)
            {
                _sportAnimTimer += dt;
                if (_sportAnimTimer >= _sportAnimDuration)
                {
                    _sportAnimTimer = -1f;
                    _sportAnimKind = SportAnimKind.None;
                }
            }
        }

        private void DetectCarryAndPetState(ref FramePose p)
        {
            // Detect active carry and cat petting early so conflicting arm overlays never fight them.
            p.carry = CarryableProp.CurrentlyHeld;
            p.carrying = p.carry != null && p.carry.UsesRig(this);
            if (p.carrying)
            {
                _carryReleaseTimer = -1f;
                _carryThrowTimer = -1f;
                _waveTimer = -1f;
            }
            float targetCarryLoad = (p.carrying && p.carry.IsAttached) ? p.carry.CarryHeaviness : 0f;
            _carryLoadBlend = Mathf.Lerp(_carryLoadBlend, targetCarryLoad, 1f - Mathf.Exp(-8f * p.dt));

            p.petHand = Vector3.zero;
            p.petCrouch = 0f;
            p.petWeight = 0f;
            p.cat = StreetCatController.ActivePet;
            p.petting = p.cat != null && p.cat.TryGetPetPose(this, out p.petCrouch, out p.petWeight, out p.petHand);
            if (p.petting)
            {
                float scaleY = transform.lossyScale.y;
                Vector3 rawPetHand = p.petHand;
                if (_hasPrevPetSample)
                {
                    float fwdMotion = Vector3.Dot(rawPetHand - _prevPetHandSample, p.cat.transform.forward);
                    float desiredReturnLift = fwdMotion > 0.0004f ? 0.024f : 0f;
                    _petReturnLift = Mathf.Lerp(_petReturnLift, desiredReturnLift, 1f - Mathf.Exp(-14f * p.dt));
                }
                else
                {
                    _petReturnLift = 0f;
                    _hasPrevPetSample = true;
                }
                _prevPetHandSample = rawPetHand;
                // Keep palm surface gliding cleanly over the cat's head->back coat, never sinking into the body.
                p.petHand += transform.up * (0.036f * scaleY + _petReturnLift);
                float catBackMinY = p.cat.transform.position.y + 0.305f * p.cat.transform.lossyScale.y;
                if (p.petHand.y < catBackMinY) p.petHand.y = catBackMinY;
                _lastPetHandWorld = p.petHand;
                _petFadeBlend = p.petWeight;
            }
            else
            {
                _hasPrevPetSample = false;
                _petReturnLift = 0f;
                if (_petFadeBlend > 0.001f)
                {
                    _petFadeBlend = Mathf.Lerp(_petFadeBlend, 0f, 1f - Mathf.Exp(-14f * p.dt));
                    if (_petFadeBlend < 0.005f) _petFadeBlend = 0f;
                }
            }
        }

        private void ComputeBaseLocomotionPose(ref FramePose p)
        {
            // Track yaw change rate of root for turning lean (+/- 10 deg) AND natural head turn anticipation!
            float currentYaw = transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(_prevYaw, currentYaw);
            _prevYaw = currentYaw;
            float yawRate = yawDelta / p.dt;
            float moveFactor = Mathf.Clamp01(_speedMs / WalkSpeedRef);
            float targetTurnLean = Mathf.Clamp(-yawRate * 0.068f, -10f, 10f) * moveFactor;
            _smoothedTurnLean = Mathf.Lerp(_smoothedTurnLean, targetTurnLean, 1f - Mathf.Exp(-8.5f * p.dt));
            float targetHeadLead = Mathf.Clamp(yawRate * 0.14f, -22f, 22f) * Mathf.Clamp01(moveFactor + 0.35f);
            _smoothedHeadTurnLead = Mathf.Lerp(_smoothedHeadTurnLead, targetHeadLead, 1f - Mathf.Exp(-9f * p.dt));

            // Track recent peak speed so stopping from normal walk/run deceleration triggers braking settle.
            if (_speedMs > _recentPeakSpeed)
            {
                _recentPeakSpeed = _speedMs;
            }
            else
            {
                _recentPeakSpeed = Mathf.MoveTowards(_recentPeakSpeed, _speedMs, 2.4f * p.dt);
            }
            if (_grounded && _recentPeakSpeed > 1.45f && _speedMs < 0.25f && _prevSpeedMs >= 0.25f && _stopSettleTimer < 0f)
            {
                _stopSettleTimer = 0f;
                _stopSettleStrength = Mathf.Clamp01((_recentPeakSpeed - 1.05f) / 3.0f);
                _recentPeakSpeed = 0f;
            }
            if (_stopSettleTimer >= 0f)
            {
                _stopSettleTimer += p.dt;
                if (_stopSettleTimer >= 0.38f || _speedMs > 0.8f)
                {
                    _stopSettleTimer = -1f;
                }
            }

            if (_talkingTimer > 0f)
            {
                _talkingTimer = Mathf.Max(0f, _talkingTimer - p.dt);
            }
            float desiredTalkBlend = (_talkingTimer > 0f && _speedMs < 0.45f && _grounded) ? 1f : 0f;
            _talkingBlend = Mathf.Lerp(_talkingBlend, desiredTalkBlend, 1f - Mathf.Exp(-7.5f * p.dt));

            // Compute idle / walk / run blends and smooth travel direction (prevents 1-frame foot pop on sharp cuts).
            p.moveBlend = Mathf.Clamp01((_speedMs - 0.08f) / 1.2f);
            p.runBlend = Mathf.InverseLerp(WalkSpeedRef, RunSpeedRef, _speedMs);

            if (_gaitWeight < 0.04f && _speedMs > 0.05f)
            {
                _smoothedTravelDir = _travelDirection;
            }
            else if (_travelDirection.sqrMagnitude > 0.0001f)
            {
                _smoothedTravelDir = Vector3.Slerp(_smoothedTravelDir, _travelDirection, 1f - Mathf.Exp(-14f * p.dt));
                if (_smoothedTravelDir.sqrMagnitude > 0.0001f) _smoothedTravelDir.Normalize();
            }
            p.localTravel = transform.InverseTransformDirection(_smoothedTravelDir);
            p.localTravel.y = 0f;
            if (p.localTravel.sqrMagnitude > 0.0001f) p.localTravel.Normalize();
            else p.localTravel = Vector3.forward;

            p.fwdDot = p.localTravel.z;
            float lateralMag = Mathf.Abs(p.localTravel.x);
            float cutIntensity = (_grounded && _speedMs > 1.0f)
                ? Mathf.Clamp01((1f - Vector3.Dot(_smoothedTravelDir, _travelDirection)) * 1.4f)
                : 0f;

            _gaitWeight = Mathf.MoveTowards(_gaitWeight, p.moveBlend, 9f * p.dt);
            p.tennisStance = _tennisRacket != null;
            p.stance = p.tennisStance ? 0.54f : Mathf.Lerp(0.61f, 0.44f, p.runBlend);
            float dirStrideScale = Mathf.Lerp(1f, 0.50f, lateralMag) * (p.fwdDot < 0f ? Mathf.Lerp(1f, 0.72f, -p.fwdDot) : 1f);
            float carryStrideScale = Mathf.Lerp(1f, 0.78f, _carryLoadBlend);
            float baseStride = (p.tennisStance ? 1.12f : Mathf.Lerp(1.22f, 1.72f, p.runBlend)) * dirStrideScale * carryStrideScale;
            p.strideLength = baseStride * Mathf.Max(0.28f, _gaitWeight);

            // Advance gait phase by distance travelled & fire footstep contact events
            if (_speedMs > 0.05f && _grounded)
            {
                _phase += _speedMs * p.dt / (p.strideLength * transform.lossyScale.y) * TwoPi;

                if (_speedMs > 0.25f)
                {
                    int currentStepCount = Mathf.FloorToInt(_phase / Mathf.PI);
                    if (currentStepCount != _lastStepCount)
                    {
                        _lastStepCount = currentStepCount;
                        OnFootstepContact?.Invoke(p.runBlend);
                    }
                }

                if (_phase > Mathf.PI * 200f)
                {
                    _phase -= Mathf.PI * 200f;
                    _lastStepCount = Mathf.FloorToInt(_phase / Mathf.PI);
                }
            }

            // Turn-in-place foot shuffle when yaw rotates while stationary
            bool canTurnInPlace = _grounded && _speedMs < 0.28f && !_isLyingDown && _prayerStage == 0 &&
                                  !_isSwimming && _kneelBlend < 0.12f && !p.petting &&
                                  _knockdownTimer < 0f && _vaultTimer < 0f;
            p.absYawRate = Mathf.Abs(yawRate);
            float desiredTurnW = (canTurnInPlace && p.absYawRate > 18f) ? Mathf.Clamp01((p.absYawRate - 18f) / 55f) : 0f;
            _turnInPlaceWeight = Mathf.Lerp(_turnInPlaceWeight, desiredTurnW,
                1f - Mathf.Exp(-(desiredTurnW > _turnInPlaceWeight ? 14f : 8.5f) * p.dt));
            if (_turnInPlaceWeight > 0.01f)
            {
                if (p.absYawRate > 12f) _turnYawSign = Mathf.Sign(yawRate);
                _turnStepPhase += Mathf.Max(p.absYawRate, 68f * desiredTurnW) * p.dt / 95f * TwoPi;
                if (_turnStepPhase > TwoPi * 100f) _turnStepPhase -= TwoPi * 100f;
            }
            else
            {
                _turnInPlaceWeight = 0f;
            }

            // --- Idle components ---
            p.breathWave = Mathf.Sin((_idleTime + _noiseSeed) * Mathf.PI * 2f * 0.25f);
            p.idleChestPitch = p.breathWave * 1.5f;
            float idleWeightShiftX = Mathf.Sin((_idleTime + _noiseSeed * 0.5f) * Mathf.PI * 2f * 0.2f) * 0.015f;
            float headNoise = (Mathf.PerlinNoise(_noiseSeed, _idleTime * 0.18f) - 0.5f) * 2f;
            float idleHeadYaw = headNoise * 10f;

            // --- Head-look IK-lite toward nearest interactable within 4 m (max 60 deg yaw) ---
            float desiredLookYaw = 0f;
            float desiredLookPitch = 0f;
            bool useLookIk = false;
            if (_hasLookPoint && _grounded && !_isLyingDown)
            {
                Vector3 localTarget = transform.InverseTransformPoint(_lookWorldPoint) - new Vector3(0f, 1.54f, 0f);
                float horizDist = new Vector2(localTarget.x, localTarget.z).magnitude;
                if (horizDist > 0.12f)
                {
                    float rawYaw = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(rawYaw) <= 100f)
                    {
                        desiredLookYaw = Mathf.Clamp(rawYaw, -60f, 60f);
                        desiredLookPitch = Mathf.Clamp(-Mathf.Atan2(localTarget.y, horizDist) * Mathf.Rad2Deg, -24f, 24f);
                        useLookIk = true;
                    }
                }
            }

            float lookBlendRate = 1f - Mathf.Exp(-7.5f * p.dt);
            _smoothedLookYaw = Mathf.Lerp(_smoothedLookYaw, useLookIk ? desiredLookYaw : 0f, lookBlendRate);
            _smoothedLookPitch = Mathf.Lerp(_smoothedLookPitch, useLookIk ? desiredLookPitch : 0f, lookBlendRate);
            _lookWeight = Mathf.Lerp(_lookWeight, useLookIk ? 1f : 0f, lookBlendRate);

            // Biomechanical arm pendulum with realistic elbow phase-lag & inward cross-body arc!
            float armDirScale = Mathf.Lerp(-0.65f, 1f, Mathf.InverseLerp(-1f, 0.25f, p.fwdDot)) * Mathf.Lerp(1f, 0.45f, lateralMag);
            float carrySwingDamp = Mathf.Lerp(1f, 0.35f, _carryLoadBlend);
            float armCycle = Mathf.Cos(_phase - 0.19f) * armDirScale * carrySwingDamp;
            float forearmLagL = Mathf.Cos(_phase - 0.62f) * armDirScale * carrySwingDamp;
            float forearmLagR = -forearmLagL;
            float armAmp = Mathf.Lerp(19f, 44f, p.runBlend);
            float elbowBaseBend = Mathf.Lerp(18f, 70f, p.runBlend);
            float walkShoulderL = armCycle * armAmp;
            float walkShoulderR = -walkShoulderL;
            float walkShoulderYawL = Mathf.Lerp(6f, 14f, p.runBlend) * Mathf.Max(0f, -armCycle);
            float walkShoulderYawR = -Mathf.Lerp(6f, 14f, p.runBlend) * Mathf.Max(0f, armCycle);
            float walkElbowL = -(elbowBaseBend + Mathf.Max(0f, -forearmLagL) * Mathf.Lerp(18f, 28f, p.runBlend));
            float walkElbowR = -(elbowBaseBend + Mathf.Max(0f, -forearmLagR) * Mathf.Lerp(18f, 28f, p.runBlend));
            float torsoForwardLean = p.fwdDot >= 0f
                ? Mathf.Lerp(3.5f, 12.5f, p.runBlend) * Mathf.Lerp(0.35f, 1f, p.fwdDot)
                : Mathf.Lerp(1f, -4.5f, -p.fwdDot);
            float strafeLeanZ = -p.localTravel.x * Mathf.Lerp(3f, 6.5f, p.runBlend) * p.moveBlend;
            float walkBobY = -Mathf.Cos(_phase * 2f) * Mathf.Lerp(0.020f, 0.042f, p.runBlend) * Mathf.Lerp(1f, 0.65f, lateralMag);
            float walkHipYaw = Mathf.Cos(_phase) * Mathf.Lerp(6.2f, 10.5f, p.runBlend) * Mathf.Lerp(1f, 0.4f, lateralMag) * carrySwingDamp;
            float walkHipRoll = -Mathf.Sin(_phase) * Mathf.Lerp(3.4f, 4.6f, p.runBlend);
            float walkHipSwayX = -Mathf.Sin(_phase) * Mathf.Lerp(0.020f, 0.015f, p.runBlend);

            // Blend Idle <-> Locomotion
            p.targetHipsPos = new Vector3(
                Mathf.Lerp(idleWeightShiftX, walkHipSwayX, p.moveBlend),
                hipsBaseY - _gaitWeight * 0.064f + walkBobY * p.moveBlend - 0.055f * cutIntensity - 0.014f * _turnInPlaceWeight,
                0f);

            float defaultHeadYaw = Mathf.Lerp(idleHeadYaw, 0f, p.moveBlend) + _smoothedHeadTurnLead * 0.65f;
            float lookHeadYaw = _smoothedLookYaw * 0.75f + _smoothedHeadTurnLead * 0.25f;
            float finalHeadYaw = Mathf.Lerp(defaultHeadYaw, lookHeadYaw, _lookWeight);

            p.targetHipsRot = new Vector3(
                0f,
                walkHipYaw * p.moveBlend - _turnYawSign * 7f * _turnInPlaceWeight,
                walkHipRoll * p.moveBlend);
            p.targetSpineRot = new Vector3(
                Mathf.Lerp(p.idleChestPitch, torsoForwardLean, p.moveBlend),
                -walkHipYaw * 1.20f * p.moveBlend,
                _smoothedTurnLean + strafeLeanZ - Mathf.Sign(p.localTravel.x) * 7f * cutIntensity - walkHipRoll * 0.72f * p.moveBlend);
            p.targetNeckRot = new Vector3(
                -p.targetSpineRot.x * 0.35f + _smoothedLookPitch * 0.25f,
                _smoothedLookYaw * 0.25f + _smoothedHeadTurnLead * 0.35f,
                -_smoothedTurnLean * 0.35f);
            p.targetHeadRot = new Vector3(_smoothedLookPitch * 0.75f, finalHeadYaw, -_smoothedTurnLean * 0.25f);

            p.targetLHipRot = new Vector3(0f, 0f, -3.2f);
            p.targetRHipRot = new Vector3(0f, 0f, 3.2f);
            p.targetLKneeRot = new Vector3(3.5f, 0f, 0f);
            p.targetRKneeRot = p.targetLKneeRot;
            p.targetLAnkleRot = Vector3.zero;
            p.targetRAnkleRot = Vector3.zero;

            float idleArmBreath = p.breathWave * 1.6f;
            p.targetLShoulderRot = new Vector3(
                Mathf.Lerp(3.5f + idleArmBreath, walkShoulderL, p.moveBlend),
                walkShoulderYawL * p.moveBlend,
                Mathf.Lerp(-9.5f, -5.2f + Mathf.Abs(armCycle) * 2.5f, p.moveBlend));
            p.targetRShoulderRot = new Vector3(
                Mathf.Lerp(3.5f + idleArmBreath, walkShoulderR, p.moveBlend),
                walkShoulderYawR * p.moveBlend,
                Mathf.Lerp(9.5f, 5.2f - Mathf.Abs(armCycle) * 2.5f, p.moveBlend));
            p.targetLElbowRot = new Vector3(Mathf.Lerp(-16f, walkElbowL, p.moveBlend), 0f, 0f);
            p.targetRElbowRot = new Vector3(Mathf.Lerp(-16f, walkElbowR, p.moveBlend), 0f, 0f);
        }

        private void ApplyIdleFidgetTalkAndAirborne(ref FramePose p)
        {
            // Braking / stopping forward-dip & rebound settle!
            if (_stopSettleTimer >= 0f && _grounded)
            {
                float sn = Mathf.Clamp01(_stopSettleTimer / 0.38f);
                float settleWave = Mathf.Sin(sn * Mathf.PI) * (1f - sn * 0.45f) * _stopSettleStrength;
                p.targetHipsPos.y -= 0.048f * settleWave;
                p.targetHipsPos.z += 0.032f * settleWave * Mathf.Sign(p.fwdDot >= -0.1f ? 1f : -1f);
                p.targetSpineRot.x += 7.0f * settleWave * (p.fwdDot >= -0.1f ? 1f : -0.6f);
                if (!p.carrying)
                {
                    p.targetLShoulderRot.x += 12f * settleWave;
                    p.targetRShoulderRot.x += 12f * settleWave;
                }
                p.targetLKneeRot.x += 12f * settleWave;
                p.targetRKneeRot.x += 12f * settleWave;
            }

            // Natural Idle Micro-Fidgets (Hands-on-hips, Neck/Shoulder Stretch, Wrist Glance) when standing idle!
            bool canFidget = p.moveBlend < 0.04f && _grounded && _talkingBlend < 0.1f && !_hasDualAction &&
                             !p.carrying && !p.petting && _consumeWeight < 0.02f && !_hasShelfReach &&
                             _carryReleaseTimer < 0f && _carryThrowTimer < 0f &&
                             _prayerStage == 0 && !_isSwimming && !_isLyingDown && _waveTimer < 0f &&
                             _sportAnimTimer < 0f && _tennisRacket == null;
            if (canFidget)
            {
                if (_idleGestureTimer >= 0f)
                {
                    _idleGestureTimer += p.dt;
                    if (_idleGestureTimer >= _idleGestureDuration)
                    {
                        _idleGestureTimer = -1f;
                        _idleGestureKind = 0;
                        _idleGestureCooldown = UnityEngine.Random.Range(7.5f, 14.5f);
                    }
                }
                else
                {
                    _idleGestureCooldown -= p.dt;
                    if (_idleGestureCooldown <= 0f)
                    {
                        _idleGestureKind = UnityEngine.Random.Range(1, 4);
                        _idleGestureTimer = 0f;
                        _idleGestureDuration = _idleGestureKind == 1 ? 3.6f : (_idleGestureKind == 2 ? 2.6f : 2.3f);
                    }
                }
            }
            else
            {
                _idleGestureTimer = -1f;
                _idleGestureKind = 0;
            }

            if (_idleGestureTimer >= 0f && _idleGestureKind > 0)
            {
                float gn = Mathf.Clamp01(_idleGestureTimer / Mathf.Max(0.2f, _idleGestureDuration));
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(gn / 0.22f)) *
                            (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((gn - 0.76f) / 0.24f)));

                if (_idleGestureKind == 1)
                {
                    // Relaxed hands-on-hips breather
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(12f, 14f, -34f), env);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(12f, -14f, 34f), env);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-86f, 0f, 24f), env);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-86f, 0f, -24f), env);
                    p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, -4f + p.breathWave * 2.5f, env);
                }
                else if (_idleGestureKind == 2)
                {
                    // Subtle neck stretch & shoulder roll
                    float tilt = Mathf.Sin(gn * Mathf.PI * 2f) * 16f;
                    p.targetHeadRot.z += tilt * env;
                    p.targetHeadRot.y += Mathf.Sin(gn * Mathf.PI) * 18f * env;
                    p.targetLShoulderRot.z -= 5f * Mathf.Sin(gn * Mathf.PI) * env;
                    p.targetRShoulderRot.z += 5f * Mathf.Sin(gn * Mathf.PI) * env;
                }
                else
                {
                    // Quick glance at wristwatch on left arm
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-44f, 28f, -12f), env);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-96f, 0f, 0f), env);
                    p.targetHeadRot.x = Mathf.Lerp(p.targetHeadRot.x, 22f, env);
                    p.targetHeadRot.y = Mathf.Lerp(p.targetHeadRot.y, -16f, env);
                }
            }

            // Natural Conversational Hand Gestures & Expressive Head Nods when speaking!
            if (_talkingBlend > 0.02f && !p.carrying && !p.petting && _consumeWeight < 0.05f && !_hasShelfReach &&
                !_hasDualAction && _waveTimer < 0f && _prayerStage == 0 && !_isLyingDown)
            {
                float t = _idleTime + _noiseSeed;
                // Phrase-modulated cadence so hands pause naturally between sentences instead of metronomic swinging
                float phrase = Mathf.SmoothStep(0.15f, 0.85f, Mathf.Sin(t * 1.45f) * 0.5f + 0.5f);
                float gR = Mathf.Sin(t * 3.6f) * phrase;
                float gL = Mathf.Sin(t * 2.7f + 1.6f) * (0.35f + 0.65f * (1f - phrase * 0.5f));
                float emphasize = Mathf.SmoothStep(0f, 1f, Mathf.Sin(t * 1.9f) * 0.5f + 0.5f);

                Vector3 talkRSh = new Vector3(-34f - gR * 20f * emphasize, -12f + gR * 12f, 16f + Mathf.Abs(gR) * 14f);
                Vector3 talkRElb = new Vector3(-68f + gR * 22f, 0f, 0f);
                Vector3 talkLSh = new Vector3(-26f - gL * 14f, 10f - gL * 9f, -14f - Mathf.Abs(gL) * 11f);
                Vector3 talkLElb = new Vector3(-64f + gL * 18f, 0f, 0f);

                p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, talkRSh, _talkingBlend);
                p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, talkRElb, _talkingBlend);
                p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, talkLSh, _talkingBlend * 0.85f);
                p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, talkLElb, _talkingBlend * 0.85f);
                p.targetSpineRot.x += (4f + Mathf.Sin(t * 2.1f) * 2.5f) * _talkingBlend;
                p.targetSpineRot.y += Mathf.Sin(t * 1.5f) * 6.5f * _talkingBlend;
                p.targetHeadRot.x += Mathf.Sin(t * 4.2f) * 7f * _talkingBlend * (0.4f + 0.6f * phrase);
                p.targetHeadRot.z += Mathf.Sin(t * 2.2f) * 5.5f * _talkingBlend;
            }
            else if (_talkingBlend > 0.02f && p.carrying && _prayerStage == 0 && !_isLyingDown)
            {
                // Keep hands firmly on the carried object; only animate expressive head nods while talking.
                float t = _idleTime + _noiseSeed;
                p.targetHeadRot.x += Mathf.Sin(t * 4.0f) * 6f * _talkingBlend;
                p.targetHeadRot.z += Mathf.Sin(t * 2.1f) * 4f * _talkingBlend;
            }

            // --- v13 polish: lean into acceleration, settle back when stopping ---
            float accel = (_speedMs - _prevSpeedMs) / p.dt;
            _prevSpeedMs = _speedMs;
            float accelDirSign = p.fwdDot >= -0.15f ? 1f : -0.7f;
            float targetAccelLean = _grounded ? Mathf.Clamp(accel * 1.4f * accelDirSign, -7f, 9f) : 0f;
            _smoothedAccelLean = Mathf.Lerp(_smoothedAccelLean, targetAccelLean, 1f - Mathf.Exp(-6f * p.dt));
            p.targetSpineRot.x += _smoothedAccelLean;
            p.targetNeckRot.x -= _smoothedAccelLean * 0.4f; // keep gaze level

            // --- v13 polish: slightly lower, more athletic pelvis while running (reduces stiff-leg float) ---
            p.targetHipsPos.y -= 0.035f * p.runBlend * p.moveBlend;

            // --- v13 polish: natural idle weight shift from leg to leg (contrapposto) every few seconds ---
            if (p.moveBlend < 0.05f && _grounded && !_isLyingDown && _turnInPlaceWeight < 0.1f)
            {
                _idleShiftCountdown -= p.dt;
                if (_idleShiftCountdown <= 0f)
                {
                    _idleShiftTarget = _idleShiftTarget == 0f ? (UnityEngine.Random.value < 0.5f ? -1f : 1f) : 0f;
                    _idleShiftCountdown = UnityEngine.Random.Range(4.5f, 9f);
                }
            }
            else
            {
                _idleShiftTarget = 0f;
            }
            _idleShift = Mathf.Lerp(_idleShift, _idleShiftTarget, 1f - Mathf.Exp(-1.8f * p.dt));
            p.idleW = _idleShift * (1f - p.moveBlend);
            p.targetHipsPos.x += p.idleW * 0.03f;
            p.targetHipsRot.z += p.idleW * 3.2f;
            p.targetSpineRot.z -= p.idleW * 2.4f;
            p.targetLKneeRot.x += Mathf.Max(0f, p.idleW) * 9f;   // relaxed knee on the unloaded leg
            p.targetRKneeRot.x += Mathf.Max(0f, -p.idleW) * 9f;
            p.targetLHipRot.x -= Mathf.Max(0f, p.idleW) * 4f;
            p.targetRHipRot.x -= Mathf.Max(0f, -p.idleW) * 4f;

            // --- Airborne override (take-off extension -> apex tuck & air balance -> landing reach) ---
            if (!_grounded)
            {
                float launchImpulse = _verticalVelocity > 1.2f
                    ? (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_airborneTimer / 0.18f)))
                    : 0f;
                float descendPrep = Mathf.Clamp01((-_verticalVelocity - 1.5f) / 5.5f);
                float apexTuck = Mathf.Clamp01(1f - launchImpulse * 0.85f - descendPrep * 0.55f);
                float airBalance = Mathf.Sin(_idleTime * 6.5f) * 8f;

                p.targetHipsPos.y = hipsBaseY + Mathf.Lerp(0.02f, 0.06f, apexTuck) - 0.04f * launchImpulse;
                p.targetLHipRot = new Vector3(Mathf.Lerp(-8f, -34f, apexTuck) + 14f * launchImpulse, 0f, -5f);
                p.targetRHipRot = new Vector3(Mathf.Lerp(-16f, -24f, apexTuck) + 10f * launchImpulse, 0f, 5f);
                p.targetLKneeRot = new Vector3(Mathf.Lerp(22f, 58f, apexTuck) * (1f - 0.55f * launchImpulse), 0f, 0f);
                p.targetRKneeRot = new Vector3(Mathf.Lerp(18f, 48f, apexTuck) * (1f - 0.55f * launchImpulse), 0f, 0f);
                p.targetLAnkleRot = new Vector3(Mathf.Lerp(12f, 26f, launchImpulse) - 10f * descendPrep, 0f, 0f);
                p.targetRAnkleRot = new Vector3(Mathf.Lerp(12f, 26f, launchImpulse) - 10f * descendPrep, 0f, 0f);

                if (!p.carrying)
                {
                    float armPitch = Mathf.Lerp(-26f, -64f, launchImpulse) + descendPrep * -18f;
                    float armOut = Mathf.Lerp(36f, 24f, launchImpulse) + descendPrep * 10f;
                    p.targetLShoulderRot = new Vector3(armPitch + airBalance, 0f, -armOut - _smoothedTurnLean * 0.8f);
                    p.targetRShoulderRot = new Vector3(armPitch - airBalance, 0f, armOut - _smoothedTurnLean * 0.8f);
                    p.targetLElbowRot = new Vector3(Mathf.Lerp(-38f, -26f, descendPrep), 0f, 0f);
                    p.targetRElbowRot = new Vector3(Mathf.Lerp(-38f, -26f, descendPrep), 0f, 0f);
                }
                p.targetSpineRot.x = Mathf.Lerp(8f, -4f, launchImpulse) + 6f * descendPrep;
            }
            // --- Jump landing squat (fast compression + smooth recovery scaled by fall impact) ---
            else if (_landSquatTimer >= 0f)
            {
                float squatNorm = Mathf.Clamp01(_landSquatTimer / Mathf.Max(0.15f, _landSquatDuration));
                float squatEnv = squatNorm < 0.28f
                    ? Mathf.SmoothStep(0f, 1f, squatNorm / 0.28f)
                    : Mathf.SmoothStep(1f, 0f, (squatNorm - 0.28f) / 0.72f);
                float impactScale = Mathf.Lerp(1f, 1.85f, _landImpact01);
                p.targetHipsPos.y -= 0.135f * impactScale * squatEnv;
                p.targetHipsPos.z -= 0.025f * impactScale * squatEnv;
                p.targetLHipRot.x -= 20f * impactScale * squatEnv;
                p.targetRHipRot.x -= 20f * impactScale * squatEnv;
                p.targetLKneeRot.x = Mathf.Max(p.targetLKneeRot.x, 38f * impactScale * squatEnv);
                p.targetRKneeRot.x = Mathf.Max(p.targetRKneeRot.x, 38f * impactScale * squatEnv);
                p.targetLAnkleRot.x -= 17f * squatEnv;
                p.targetRAnkleRot.x -= 17f * squatEnv;
                p.targetSpineRot.x += Mathf.Lerp(10f, 20f, _landImpact01) * squatEnv;
                if (!p.carrying)
                {
                    p.targetLShoulderRot.x = Mathf.Lerp(p.targetLShoulderRot.x, 12f, squatEnv * 0.55f);
                    p.targetRShoulderRot.x = Mathf.Lerp(p.targetRShoulderRot.x, 12f, squatEnv * 0.55f);
                    p.targetLShoulderRot.z -= 16f * squatEnv;
                    p.targetRShoulderRot.z += 16f * squatEnv;
                    p.targetLElbowRot.x = Mathf.Lerp(p.targetLElbowRot.x, -42f, squatEnv * 0.65f);
                    p.targetRElbowRot.x = Mathf.Lerp(p.targetRElbowRot.x, -42f, squatEnv * 0.65f);
                }
            }

            // --- v13 polish: arms flail and legs reach smoothly when falling from height ---
            if (!_grounded && _verticalVelocity < -3.5f && _airborneTimer > 0.30f)
            {
                float fallTimeRamp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_airborneTimer - 0.30f) / 0.22f));
                float fall = Mathf.Clamp01((-_verticalVelocity - 3.5f) / 6f) * fallTimeRamp;
                float flail = Mathf.Sin(_idleTime * 14f);
                if (!p.carrying)
                {
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-95f + flail * 18f, 0f, -55f), fall);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-95f - flail * 18f, 0f, 55f), fall);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-20f, 0f, 0f), fall);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-20f, 0f, 0f), fall);
                }
                p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(-10f - flail * 8f, 0f, -6f), fall);
                p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(-10f + flail * 8f, 0f, 6f), fall);
                p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(22f, 0f, 0f), fall);
                p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(22f, 0f, 0f), fall);
                p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, -6f, fall);
            }

            p.knockdownFootBlend = 1f;
            p.startledStepW = 0f;
            p.hitStaggerW = 0f;
        }

        private void ApplyActivityOverrides(ref FramePose p)
        {
            if (_isLyingDown)
            {
                ApplyLyingDownPose(ref p);
            }
            else if (_prayerStage > 0)
            {
                ApplyPrayerPose(ref p);
            }
            else if (_isSwimming)
            {
                ApplySwimmingPose(ref p);
            }
            else if (_vaultTimer >= 0f)
            {
                ApplyVaultPose(ref p);
            }
            else if (_knockdownTimer >= 0f)
            {
                ApplyKnockdownPose(ref p);
            }
            else if (_hitReactTimer >= 0f)
            {
                ApplyHitReactPose(ref p);
            }
            else if (_punchTimer >= 0f)
            {
                ApplyCombatComboPose(ref p);
            }
            else if (_startledTimer >= 0f)
            {
                ApplyStartledPose(ref p);
            }
            else if (_waveTimer >= 0f && !p.carrying && !p.petting && _consumeWeight < 0.05f && !_hasShelfReach && !_hasDualAction)
            {
                ApplyWavePose(ref p);
            }
        }

        // --- Bed sleep lying-down override (Stand -> SitOnMattress -> Lie variant + breathing + stirring) ---
        private void ApplyLyingDownPose(ref FramePose p)
        {
            float e = _lyingBlend;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / 0.45f));
            float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((e - 0.45f) / 0.55f));

            // Intermediate pose: sitting upright on the mattress, legs straight forward, hands beside hips
            Vector3 sitHipsPos = new Vector3(0f, 0.22f, 0f);
            Vector3 sitHipsRot = Vector3.zero;
            Vector3 sitSpine = new Vector3(-4f, 0f, 0f);
            Vector3 sitNeck = new Vector3(4f, 0f, 0f);
            Vector3 sitHead = new Vector3(6f, 0f, 0f);
            Vector3 sitLHip = new Vector3(-86f, 0f, -5f);
            Vector3 sitRHip = new Vector3(-86f, 0f, 5f);
            Vector3 sitLKnee = new Vector3(12f, 0f, 0f);
            Vector3 sitRKnee = new Vector3(12f, 0f, 0f);
            Vector3 sitLAnkle = new Vector3(10f, 0f, 0f);
            Vector3 sitRAnkle = new Vector3(10f, 0f, 0f);
            Vector3 sitLSh = new Vector3(14f, 0f, -14f);
            Vector3 sitRSh = new Vector3(14f, 0f, 14f);
            Vector3 sitLElb = new Vector3(-30f, 0f, 0f);
            Vector3 sitRElb = new Vector3(-30f, 0f, 0f);

            // Final lying pose (Variant 0 default: on back, hands resting on belly)
            Vector3 lieHipsPos = new Vector3(0f, 0.16f, 0f);
            Vector3 lieHipsRot = new Vector3(-90f, 0f, 0f);
            Vector3 lieSpine = new Vector3(2f, 0f, 0f);
            Vector3 lieNeck = new Vector3(6f, 0f, 0f);
            Vector3 lieHead = new Vector3(10f, 6f, 0f);
            Vector3 lieLSh = new Vector3(-22f, 0f, -6f);
            Vector3 lieRSh = new Vector3(-22f, 0f, 6f);
            Vector3 lieLElb = new Vector3(-78f, 0f, 14f);
            Vector3 lieRElb = new Vector3(-78f, 0f, -14f);
            Vector3 lieLHip = new Vector3(2f, 0f, -4f);
            Vector3 lieRHip = new Vector3(2f, 0f, 4f);
            Vector3 lieLKnee = new Vector3(6f, 0f, 0f);
            Vector3 lieRKnee = new Vector3(6f, 0f, 0f);
            Vector3 lieLAnkle = new Vector3(18f, 0f, 0f);
            Vector3 lieRAnkle = new Vector3(18f, 0f, 0f);

            if (_lyingVariant == 1)
            {
                // On back, right knee raised, right hand resting behind head
                lieRHip = new Vector3(-42f, 0f, 6f);
                lieRKnee = new Vector3(78f, 0f, 0f);
                lieRAnkle = new Vector3(-6f, 0f, 0f);
                lieRSh = new Vector3(-158f, 0f, 28f);
                lieRElb = new Vector3(-118f, 0f, 0f);
                lieHead = new Vector3(8f, 14f, 0f);
            }
            else if (_lyingVariant == 2)
            {
                // Slightly turned onto the right side, knees gently bent, left arm across body
                lieHipsPos = new Vector3(0f, 0.19f, 0f);
                lieHipsRot = new Vector3(-90f, 0f, 24f);
                lieSpine = new Vector3(2f, 0f, 10f);
                lieHead = new Vector3(8f, -10f, 6f);
                lieLHip = new Vector3(-28f, 0f, -2f);
                lieRHip = new Vector3(-20f, 0f, 4f);
                lieLKnee = new Vector3(42f, 0f, 0f);
                lieRKnee = new Vector3(34f, 0f, 0f);
                lieLAnkle = new Vector3(14f, 0f, 0f);
                lieRAnkle = new Vector3(14f, 0f, 0f);
                lieLSh = new Vector3(-48f, 0f, -4f);
                lieLElb = new Vector3(-62f, 0f, 10f);
                lieRSh = new Vector3(-8f, 0f, 16f);
                lieRElb = new Vector3(-24f, 0f, 0f);
            }

            p.targetHipsPos = Vector3.Lerp(Vector3.Lerp(p.targetHipsPos, sitHipsPos, a), lieHipsPos, b);
            p.targetHipsRot = Vector3.Lerp(Vector3.Lerp(p.targetHipsRot, sitHipsRot, a), lieHipsRot, b);
            p.targetSpineRot = Vector3.Lerp(Vector3.Lerp(p.targetSpineRot, sitSpine, a), lieSpine, b);
            p.targetNeckRot = Vector3.Lerp(Vector3.Lerp(p.targetNeckRot, sitNeck, a), lieNeck, b);
            p.targetHeadRot = Vector3.Lerp(Vector3.Lerp(p.targetHeadRot, sitHead, a), lieHead, b);
            p.targetLShoulderRot = Vector3.Lerp(Vector3.Lerp(p.targetLShoulderRot, sitLSh, a), lieLSh, b);
            p.targetRShoulderRot = Vector3.Lerp(Vector3.Lerp(p.targetRShoulderRot, sitRSh, a), lieRSh, b);
            p.targetLElbowRot = Vector3.Lerp(Vector3.Lerp(p.targetLElbowRot, sitLElb, a), lieLElb, b);
            p.targetRElbowRot = Vector3.Lerp(Vector3.Lerp(p.targetRElbowRot, sitRElb, a), lieRElb, b);
            p.targetLHipRot = Vector3.Lerp(Vector3.Lerp(p.targetLHipRot, sitLHip, a), lieLHip, b);
            p.targetRHipRot = Vector3.Lerp(Vector3.Lerp(p.targetRHipRot, sitRHip, a), lieRHip, b);
            p.targetLKneeRot = Vector3.Lerp(Vector3.Lerp(p.targetLKneeRot, sitLKnee, a), lieLKnee, b);
            p.targetRKneeRot = Vector3.Lerp(Vector3.Lerp(p.targetRKneeRot, sitRKnee, a), lieRKnee, b);
            p.targetLAnkleRot = Vector3.Lerp(Vector3.Lerp(p.targetLAnkleRot, sitLAnkle, a), lieLAnkle, b);
            p.targetRAnkleRot = Vector3.Lerp(Vector3.Lerp(p.targetRAnkleRot, sitRAnkle, a), lieRAnkle, b);

            // Slow sleep breathing scaled by lying stage
            float lieBreathArg = (_idleTime + _noiseSeed) * TwoPi * 0.22f;
            p.targetSpineRot.x += Mathf.Sin(lieBreathArg) * 1.8f * b;
            p.targetHeadRot.x += Mathf.Sin(lieBreathArg - 0.6f) * 0.8f * b;

            // Periodic gentle sleep stir (head turn, knee flex & subtle torso roll)
            if (_lyingStirTimer >= 0f)
            {
                float s = Mathf.Clamp01(_lyingStirTimer / 1.8f);
                float w = Mathf.Sin(s * Mathf.PI) * b;
                p.targetHeadRot.y += 18f * _lyingStirSide * w;
                if (_lyingStirSide < 0f)
                {
                    p.targetLKneeRot.x += 16f * w;
                    p.targetLHipRot.x -= 10f * w;
                }
                else
                {
                    p.targetRKneeRot.x += 16f * w;
                    p.targetRHipRot.x -= 10f * w;
                }
                p.targetSpineRot.z += 4f * _lyingStirSide * w;
                _lyingStirTimer += p.dt;
                if (_lyingStirTimer >= 1.8f)
                {
                    _lyingStirTimer = -1f;
                }
            }
        }

        // --- Prayer (Namaz / Salah) mathematically exact pose override (Tekbir -> Kıyam -> Rükû -> Kavme -> Secde -> Oturuş/Selam) ---
        private void ApplyPrayerPose(ref FramePose p)
        {
            if (_prayerStage == 1 || _prayerStage == 5 || _prayerStage == 6)
            {
                // Standing phases: 5 = Tekbir (hands beside ears via IK), 1 = Kıyam (hands folded over abdomen via IK), 6 = Kavme (arms at sides)
                p.targetHipsPos = new Vector3(0f, hipsBaseY, 0f);
                p.targetHipsRot = Vector3.zero;
                p.targetSpineRot = new Vector3(2f + p.idleChestPitch * 0.35f, 0f, 0f);
                p.targetNeckRot = new Vector3(7f, 0f, 0f);
                p.targetHeadRot = new Vector3(11f, 0f, 0f);
                p.targetLShoulderRot = new Vector3(4f, 0f, -6f);
                p.targetRShoulderRot = new Vector3(4f, 0f, 6f);
                p.targetLElbowRot = new Vector3(-10f, 0f, 0f);
                p.targetRElbowRot = new Vector3(-10f, 0f, 0f);
                p.targetLHipRot = new Vector3(0f, 0f, -2f);
                p.targetRHipRot = new Vector3(0f, 0f, 2f);
                p.targetLKneeRot = new Vector3(2f, 0f, 0f);
                p.targetRKneeRot = new Vector3(2f, 0f, 0f);
                p.targetLAnkleRot = Vector3.zero;
                p.targetRAnkleRot = Vector3.zero;
            }
            else if (_prayerStage == 2)
            {
                // Rükû: crisp straight 88° horizontal back, head aligned with spine, palms placed directly on knees via IK
                p.targetHipsPos = new Vector3(0f, hipsBaseY - 0.025f, -0.06f);
                p.targetHipsRot = new Vector3(12f, 0f, 0f);
                p.targetSpineRot = new Vector3(76f, 0f, 0f);
                p.targetNeckRot = new Vector3(-4f, 0f, 0f);
                p.targetHeadRot = new Vector3(-4f, 0f, 0f);
                p.targetLShoulderRot = new Vector3(-78f, 0f, -6f);
                p.targetRShoulderRot = new Vector3(-78f, 0f, 6f);
                p.targetLElbowRot = new Vector3(-10f, 0f, 0f);
                p.targetRElbowRot = new Vector3(-10f, 0f, 0f);
                p.targetLHipRot = new Vector3(-12f, 0f, -2f);
                p.targetRHipRot = new Vector3(-12f, 0f, 2f);
                p.targetLKneeRot = new Vector3(3f, 0f, 0f);
                p.targetRKneeRot = new Vector3(3f, 0f, 0f);
                p.targetLAnkleRot = Vector3.zero;
                p.targetRAnkleRot = Vector3.zero;
            }
            else if (_prayerStage == 3)
            {
                // Secde: exact 7-point prostration — knees (y≈0.058), toes, forehead (y≈0.03), and both palms (via IK) resting cleanly on the carpet!
                p.targetHipsPos = new Vector3(0f, 0.45f, -0.14f);
                p.targetHipsRot = new Vector3(62f, 0f, 0f);
                p.targetSpineRot = new Vector3(64f, 0f, 0f);
                p.targetNeckRot = new Vector3(-10f, 0f, 0f);
                p.targetHeadRot = new Vector3(-12f, 0f, 0f);
                p.targetLShoulderRot = new Vector3(-105f, 0f, -18f);
                p.targetRShoulderRot = new Vector3(-105f, 0f, 18f);
                p.targetLElbowRot = new Vector3(-55f, 0f, 0f);
                p.targetRElbowRot = new Vector3(-55f, 0f, 0f);
                p.targetLHipRot = new Vector3(-92f, 0f, -5f);
                p.targetRHipRot = new Vector3(-92f, 0f, 5f);
                p.targetLKneeRot = new Vector3(122f, 0f, 0f);
                p.targetRKneeRot = new Vector3(122f, 0f, 0f);
                p.targetLAnkleRot = new Vector3(-62f, 0f, 0f);
                p.targetRAnkleRot = new Vector3(-62f, 0f, 0f);
            }
            else
            {
                // Oturuş / Celse / Tahiyyat & Selam: kneeling back on heels with knees & shins flat on carpet (y≈0.059), upright torso, palms on thighs via IK
                p.targetHipsPos = new Vector3(0f, 0.24f, -0.18f);
                p.targetHipsRot = new Vector3(8f, 0f, 0f);
                p.targetSpineRot = new Vector3(-5f + p.idleChestPitch * 0.3f, 0f, 0f);
                p.targetNeckRot = new Vector3(5f, _prayerSelamYaw * 0.38f, 0f);
                p.targetHeadRot = new Vector3(9f, _prayerSelamYaw * 0.62f, 0f);
                p.targetLShoulderRot = new Vector3(-28f, 0f, -6f);
                p.targetRShoulderRot = new Vector3(-28f, 0f, 6f);
                p.targetLElbowRot = new Vector3(-24f, 0f, 0f);
                p.targetRElbowRot = new Vector3(-24f, 0f, 0f);
                p.targetLHipRot = new Vector3(-76f, 0f, -5f);
                p.targetRHipRot = new Vector3(-76f, 0f, 5f);
                p.targetLKneeRot = new Vector3(158f, 0f, 0f);
                p.targetRKneeRot = new Vector3(158f, 0f, 0f);
                p.targetLAnkleRot = new Vector3(56f, 0f, 0f);
                p.targetRAnkleRot = new Vector3(56f, 0f, 0f);
            }
        }

        // --- Swimming override (Freestyle front-crawl or Breaststroke Kurbağalama!) ---
        private void ApplySwimmingPose(ref FramePose p)
        {
            float swimBlend = Mathf.Clamp01(_swimSpeed / 1.8f);
            _swimPhase += p.dt * Mathf.Lerp(2.8f, _swimBreaststroke ? 4.8f : 6.4f, swimBlend);

            if (_swimBreaststroke)
            {
                float pull = Mathf.Sin(_swimPhase);
                float sweep = Mathf.Cos(_swimPhase);
                p.targetHipsPos = new Vector3(0f, Mathf.Lerp(0.56f, 0.70f, swimBlend) + pull * 0.06f, 0f);
                p.targetHipsRot = new Vector3(Mathf.Lerp(18f, 68f, swimBlend) - pull * 8f, 0f, 0f);
                p.targetSpineRot = new Vector3(Mathf.Lerp(-8f, -14f - pull * 10f, swimBlend), 0f, 0f);
                p.targetNeckRot = new Vector3(Mathf.Lerp(-8f, -34f, swimBlend), 0f, 0f);
                p.targetHeadRot = new Vector3(Mathf.Lerp(-6f, -18f, swimBlend), 0f, 0f);

                float armPitch = Mathf.Lerp(-55f, -142f + pull * 42f, swimBlend);
                float armSpread = Mathf.Lerp(26f, 18f + Mathf.Max(0f, sweep) * 58f, swimBlend);
                p.targetLShoulderRot = new Vector3(armPitch, 0f, -armSpread);
                p.targetRShoulderRot = new Vector3(armPitch, 0f, armSpread);
                p.targetLElbowRot = new Vector3(Mathf.Lerp(-44f, -22f - Mathf.Max(0f, -pull) * 48f, swimBlend), 0f, 0f);
                p.targetRElbowRot = new Vector3(Mathf.Lerp(-44f, -22f - Mathf.Max(0f, -pull) * 48f, swimBlend), 0f, 0f);

                // Frog kick synchronized with glide!
                float frog = Mathf.Max(0f, -pull);
                p.targetLHipRot = new Vector3(Mathf.Lerp(-14f, -28f * frog + 12f, swimBlend), -18f * frog, -10f - 18f * frog);
                p.targetRHipRot = new Vector3(Mathf.Lerp(-14f, -28f * frog + 12f, swimBlend), 18f * frog, 10f + 18f * frog);
                p.targetLKneeRot = new Vector3(Mathf.Lerp(32f, 16f + frog * 74f, swimBlend), 0f, 0f);
                p.targetRKneeRot = new Vector3(Mathf.Lerp(32f, 16f + frog * 74f, swimBlend), 0f, 0f);
                p.targetLAnkleRot = new Vector3(24f, 0f, 0f);
                p.targetRAnkleRot = new Vector3(24f, 0f, 0f);
            }
            else
            {
                float strokeL = Mathf.Sin(_swimPhase);
                float strokeR = Mathf.Sin(_swimPhase + Mathf.PI);

                p.targetHipsPos = new Vector3(0f, Mathf.Lerp(0.56f, 0.72f, swimBlend) + Mathf.Sin(_swimPhase * 0.5f) * 0.04f, 0f);
                p.targetHipsRot = new Vector3(Mathf.Lerp(18f, 74f, swimBlend), 0f, strokeL * 14f * swimBlend);
                p.targetSpineRot = new Vector3(Mathf.Lerp(-8f, -18f, swimBlend), -strokeL * 18f * swimBlend, 0f);
                p.targetNeckRot = new Vector3(Mathf.Lerp(-8f, -38f, swimBlend), 0f, 0f);
                p.targetHeadRot = new Vector3(Mathf.Lerp(-6f, -20f, swimBlend), 0f, 0f);

                p.targetLShoulderRot = new Vector3(Mathf.Lerp(-55f, -155f + strokeL * 78f, swimBlend), 0f, Mathf.Lerp(-36f + strokeL * 24f, -22f, swimBlend));
                p.targetRShoulderRot = new Vector3(Mathf.Lerp(-55f, -155f + strokeR * 78f, swimBlend), 0f, Mathf.Lerp(36f - strokeR * 24f, 22f, swimBlend));
                p.targetLElbowRot = new Vector3(Mathf.Lerp(-48f, -28f - Mathf.Max(0f, strokeL) * 45f, swimBlend), 0f, 0f);
                p.targetRElbowRot = new Vector3(Mathf.Lerp(-48f, -28f - Mathf.Max(0f, strokeR) * 45f, swimBlend), 0f, 0f);

                float kickL = Mathf.Sin(_swimPhase * 2.2f);
                float kickR = Mathf.Sin(_swimPhase * 2.2f + Mathf.PI);
                p.targetLHipRot = new Vector3(Mathf.Lerp(-16f, 10f + kickL * 22f, swimBlend), 0f, -5f);
                p.targetRHipRot = new Vector3(Mathf.Lerp(-16f, 10f + kickR * 22f, swimBlend), 0f, 5f);
                p.targetLKneeRot = new Vector3(Mathf.Lerp(36f, 18f + Mathf.Max(0f, -kickL) * 26f, swimBlend), 0f, 0f);
                p.targetRKneeRot = new Vector3(Mathf.Lerp(36f, 18f + Mathf.Max(0f, -kickR) * 26f, swimBlend), 0f, 0f);
                p.targetLAnkleRot = new Vector3(28f, 0f, 0f);
                p.targetRAnkleRot = new Vector3(28f, 0f, 0f);
            }
        }

        // --- Parkour Vault hurdle animation (0.42 s) ---
        private void ApplyVaultPose(ref FramePose p)
        {
            float vNorm = Mathf.Clamp01(_vaultTimer / VaultDuration);
            float vEnv = Mathf.Sin(vNorm * Mathf.PI);
            p.targetHipsPos.y = Mathf.Lerp(p.targetHipsPos.y, hipsBaseY - 0.11f, vEnv);
            p.targetSpineRot = Vector3.Lerp(p.targetSpineRot, new Vector3(49f, -12f, 14f), vEnv);
            p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-86f, 12f, -16f), vEnv);
            p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-72f, -12f, 24f), vEnv);
            p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-24f, 0f, 0f), vEnv);
            p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-34f, 0f, 0f), vEnv);
            float leadingLeg = Mathf.Sin(Mathf.Clamp01(vNorm * 1.24f) * Mathf.PI);
            float trailingLeg = Mathf.Sin(Mathf.Clamp01((vNorm - 0.15f) * 1.17f) * Mathf.PI);
            p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(-89f, 0f, -14f), leadingLeg);
            p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(-72f, 0f, 18f), trailingLeg);
            p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(105f, 0f, 0f), vEnv);
            p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(95f, 0f, 0f), vEnv);
        }

        // --- Full NPC Knockdown onto back & multi-stage recovery (Fall -> Lie -> Sit-Up -> Kneel -> Stand) ---
        private void ApplyKnockdownPose(ref FramePose p)
        {
            float totalDur = Mathf.Max(1.2f, _activeKnockdownDuration);
            float fallDur = Mathf.Min(0.36f, totalDur * 0.22f);
            float getUpDur = Mathf.Min(1.26f, totalDur * 0.52f);
            float getUpStart = totalDur - getUpDur;

            // Stand baseline captured before override so final stand stage converges seamlessly to standing pose
            Vector3 standHipsPos = p.targetHipsPos;
            Vector3 standHipsRot = p.targetHipsRot;
            Vector3 standSpine = p.targetSpineRot;
            Vector3 standNeck = p.targetNeckRot;
            Vector3 standHead = p.targetHeadRot;
            Vector3 standLSh = p.targetLShoulderRot;
            Vector3 standRSh = p.targetRShoulderRot;
            Vector3 standLElb = p.targetLElbowRot;
            Vector3 standRElb = p.targetRElbowRot;
            Vector3 standLHip = p.targetLHipRot;
            Vector3 standRHip = p.targetRHipRot;
            Vector3 standLKnee = p.targetLKneeRot;
            Vector3 standRKnee = p.targetRKneeRot;
            Vector3 standLAnkle = p.targetLAnkleRot;
            Vector3 standRAnkle = p.targetRAnkleRot;

            // Keyframe 1: Supine knockdown on back
            float lieBreath = Mathf.Sin((_idleTime + _noiseSeed) * 3.2f) * 2.2f;
            Vector3 lieHipsPos = new Vector3(0f, 0.20f, -0.18f);
            Vector3 lieHipsRot = new Vector3(-86f, 8f, 6f);
            Vector3 lieSpine = new Vector3(10f + lieBreath, 0f, 0f);
            Vector3 lieNeck = new Vector3(8f, 0f, 0f);
            Vector3 lieHead = new Vector3(16f, -14f, 0f);
            Vector3 lieLSh = new Vector3(-25f, 0f, -62f);
            Vector3 lieRSh = new Vector3(-18f, 0f, 58f);
            Vector3 lieLElb = new Vector3(-28f, 0f, 0f);
            Vector3 lieRElb = new Vector3(-34f, 0f, 0f);
            Vector3 lieLHip = new Vector3(6f, 0f, -14f);
            Vector3 lieRHip = new Vector3(-12f, 0f, 16f);
            Vector3 lieLKnee = new Vector3(18f, 0f, 0f);
            Vector3 lieRKnee = new Vector3(32f, 0f, 0f);
            Vector3 lieLAnkle = new Vector3(18f, 0f, 0f);
            Vector3 lieRAnkle = new Vector3(18f, 0f, 0f);

            // Keyframe 2: Sit-up on ground with knees bent & hands pushing off floor
            Vector3 sitHipsPos = new Vector3(0f, 0.21f, -0.06f);
            Vector3 sitHipsRot = new Vector3(-6f, 4f, 0f);
            Vector3 sitSpine = new Vector3(20f, -4f, 0f);
            Vector3 sitNeck = new Vector3(4f, 0f, 0f);
            Vector3 sitHead = new Vector3(14f, 0f, 0f);
            Vector3 sitLSh = new Vector3(26f, 0f, -24f);
            Vector3 sitRSh = new Vector3(22f, 0f, 26f);
            Vector3 sitLElb = new Vector3(-26f, 0f, 0f);
            Vector3 sitRElb = new Vector3(-26f, 0f, 0f);
            Vector3 sitLHip = new Vector3(-126f, 0f, -8f);
            Vector3 sitRHip = new Vector3(-118f, 0f, 10f);
            Vector3 sitLKnee = new Vector3(106f, 0f, 0f);
            Vector3 sitRKnee = new Vector3(102f, 0f, 0f);
            Vector3 sitLAnkle = new Vector3(-8f, 0f, 0f);
            Vector3 sitRAnkle = new Vector3(-4f, 0f, 0f);

            // Keyframe 3: One-knee kneel (left foot planted forward, right knee down, left hand bracing on knee)
            Vector3 kneelHipsPos = new Vector3(0f, 0.57f, 0.04f);
            Vector3 kneelHipsRot = new Vector3(6f, -6f, 0f);
            Vector3 kneelSpine = new Vector3(24f, 6f, 0f);
            Vector3 kneelNeck = new Vector3(-6f, 0f, 0f);
            Vector3 kneelHead = new Vector3(10f, 0f, 0f);
            Vector3 kneelLSh = new Vector3(-48f, 12f, -10f);
            Vector3 kneelRSh = new Vector3(-26f, 0f, 22f);
            Vector3 kneelLElb = new Vector3(-44f, 0f, 0f);
            Vector3 kneelRElb = new Vector3(-38f, 0f, 0f);
            Vector3 kneelLHip = new Vector3(-82f, 0f, -6f);
            Vector3 kneelRHip = new Vector3(12f, 0f, 6f);
            Vector3 kneelLKnee = new Vector3(82f, 0f, 0f);
            Vector3 kneelRKnee = new Vector3(86f, 0f, 0f);
            Vector3 kneelLAnkle = new Vector3(-6f, 0f, 0f);
            Vector3 kneelRAnkle = new Vector3(48f, 0f, 0f);

            if (_knockdownTimer < fallDur)
            {
                p.knockdownFootBlend = 0f;
                float fallU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_knockdownTimer / fallDur));
                float bounce = Mathf.Sin(Mathf.Clamp01((_knockdownTimer / fallDur - 0.72f) / 0.28f) * Mathf.PI) * 0.03f;
                p.targetHipsPos = Vector3.Lerp(standHipsPos, lieHipsPos + Vector3.up * bounce, fallU);
                p.targetHipsRot = Vector3.Lerp(standHipsRot, lieHipsRot, fallU);
                p.targetSpineRot = Vector3.Lerp(standSpine, lieSpine, fallU);
                p.targetNeckRot = Vector3.Lerp(standNeck, lieNeck, fallU);
                p.targetHeadRot = Vector3.Lerp(standHead, lieHead, fallU);
                p.targetLShoulderRot = Vector3.Lerp(standLSh, lieLSh, fallU);
                p.targetRShoulderRot = Vector3.Lerp(standRSh, lieRSh, fallU);
                p.targetLElbowRot = Vector3.Lerp(standLElb, lieLElb, fallU);
                p.targetRElbowRot = Vector3.Lerp(standRElb, lieRElb, fallU);
                p.targetLHipRot = Vector3.Lerp(standLHip, lieLHip, fallU);
                p.targetRHipRot = Vector3.Lerp(standRHip, lieRHip, fallU);
                p.targetLKneeRot = Vector3.Lerp(standLKnee, lieLKnee, fallU);
                p.targetRKneeRot = Vector3.Lerp(standRKnee, lieRKnee, fallU);
                p.targetLAnkleRot = Vector3.Lerp(standLAnkle, lieLAnkle, fallU);
                p.targetRAnkleRot = Vector3.Lerp(standRAnkle, lieRAnkle, fallU);
            }
            else if (_knockdownTimer < getUpStart)
            {
                p.knockdownFootBlend = 0f;
                p.targetHipsPos = lieHipsPos;
                p.targetHipsRot = lieHipsRot;
                p.targetSpineRot = lieSpine;
                p.targetNeckRot = lieNeck;
                p.targetHeadRot = lieHead;
                p.targetLShoulderRot = lieLSh;
                p.targetRShoulderRot = lieRSh;
                p.targetLElbowRot = lieLElb;
                p.targetRElbowRot = lieRElb;
                p.targetLHipRot = lieLHip;
                p.targetRHipRot = lieRHip;
                p.targetLKneeRot = lieLKnee;
                p.targetRKneeRot = lieRKnee;
                p.targetLAnkleRot = lieLAnkle;
                p.targetRAnkleRot = lieRAnkle;
            }
            else
            {
                float gNorm = Mathf.Clamp01((_knockdownTimer - getUpStart) / Mathf.Max(0.25f, getUpDur));
                if (gNorm < 0.34f)
                {
                    // Stage A: Lie -> Sit-Up
                    p.knockdownFootBlend = 0f;
                    float u = Mathf.SmoothStep(0f, 1f, gNorm / 0.34f);
                    p.targetHipsPos = Vector3.Lerp(lieHipsPos, sitHipsPos, u);
                    p.targetHipsRot = Vector3.Lerp(lieHipsRot, sitHipsRot, u);
                    p.targetSpineRot = Vector3.Lerp(lieSpine, sitSpine, u);
                    p.targetNeckRot = Vector3.Lerp(lieNeck, sitNeck, u);
                    p.targetHeadRot = Vector3.Lerp(lieHead, sitHead, u);
                    p.targetLShoulderRot = Vector3.Lerp(lieLSh, sitLSh, u);
                    p.targetRShoulderRot = Vector3.Lerp(lieRSh, sitRSh, u);
                    p.targetLElbowRot = Vector3.Lerp(lieLElb, sitLElb, u);
                    p.targetRElbowRot = Vector3.Lerp(lieRElb, sitRElb, u);
                    p.targetLHipRot = Vector3.Lerp(lieLHip, sitLHip, u);
                    p.targetRHipRot = Vector3.Lerp(lieRHip, sitRHip, u);
                    p.targetLKneeRot = Vector3.Lerp(lieLKnee, sitLKnee, u);
                    p.targetRKneeRot = Vector3.Lerp(lieRKnee, sitRKnee, u);
                    p.targetLAnkleRot = Vector3.Lerp(lieLAnkle, sitLAnkle, u);
                    p.targetRAnkleRot = Vector3.Lerp(lieRAnkle, sitRAnkle, u);
                }
                else if (gNorm < 0.68f)
                {
                    // Stage B: Sit-Up -> One-Knee Kneel
                    p.knockdownFootBlend = 0f;
                    float u = Mathf.SmoothStep(0f, 1f, (gNorm - 0.34f) / 0.34f);
                    p.targetHipsPos = Vector3.Lerp(sitHipsPos, kneelHipsPos, u);
                    p.targetHipsRot = Vector3.Lerp(sitHipsRot, kneelHipsRot, u);
                    p.targetSpineRot = Vector3.Lerp(sitSpine, kneelSpine, u);
                    p.targetNeckRot = Vector3.Lerp(sitNeck, kneelNeck, u);
                    p.targetHeadRot = Vector3.Lerp(sitHead, kneelHead, u);
                    p.targetLShoulderRot = Vector3.Lerp(sitLSh, kneelLSh, u);
                    p.targetRShoulderRot = Vector3.Lerp(sitRSh, kneelRSh, u);
                    p.targetLElbowRot = Vector3.Lerp(sitLElb, kneelLElb, u);
                    p.targetRElbowRot = Vector3.Lerp(sitRElb, kneelRElb, u);
                    p.targetLHipRot = Vector3.Lerp(sitLHip, kneelLHip, u);
                    p.targetRHipRot = Vector3.Lerp(sitRHip, kneelRHip, u);
                    p.targetLKneeRot = Vector3.Lerp(sitLKnee, kneelLKnee, u);
                    p.targetRKneeRot = Vector3.Lerp(sitRKnee, kneelRKnee, u);
                    p.targetLAnkleRot = Vector3.Lerp(sitLAnkle, kneelLAnkle, u);
                    p.targetRAnkleRot = Vector3.Lerp(sitRAnkle, kneelRAnkle, u);
                }
                else
                {
                    // Stage C: One-Knee Kneel -> Stand-Up with smooth PlantFeet handoff (zero end pop)
                    float standU = Mathf.Clamp01((gNorm - 0.68f) / 0.32f);
                    float u = Mathf.SmoothStep(0f, 1f, standU);
                    p.knockdownFootBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((standU - 0.28f) / 0.72f));
                    float riseArch = Mathf.Sin(u * Mathf.PI);
                    p.targetHipsPos = Vector3.Lerp(kneelHipsPos, standHipsPos, u);
                    p.targetHipsRot = Vector3.Lerp(kneelHipsRot, standHipsRot, u);
                    p.targetSpineRot = Vector3.Lerp(kneelSpine, standSpine, u) + new Vector3(6f * riseArch, 0f, 0f);
                    p.targetNeckRot = Vector3.Lerp(kneelNeck, standNeck, u);
                    p.targetHeadRot = Vector3.Lerp(kneelHead, standHead, u);
                    p.targetLShoulderRot = Vector3.Lerp(kneelLSh, standLSh, u);
                    p.targetRShoulderRot = Vector3.Lerp(kneelRSh, standRSh, u);
                    p.targetLElbowRot = Vector3.Lerp(kneelLElb, standLElb, u);
                    p.targetRElbowRot = Vector3.Lerp(kneelRElb, standRElb, u);
                    p.targetLHipRot = Vector3.Lerp(kneelLHip, standLHip, u);
                    p.targetRHipRot = Vector3.Lerp(kneelRHip, standRHip, u);
                    p.targetLKneeRot = Vector3.Lerp(kneelLKnee, standLKnee, u);
                    p.targetRKneeRot = Vector3.Lerp(kneelRKnee, standRKnee, u);
                    p.targetLAnkleRot = Vector3.Lerp(kneelLAnkle, standLAnkle, u);
                    p.targetRAnkleRot = Vector3.Lerp(kneelRAnkle, standRAnkle, u);
                }
            }
        }

        // --- Hit recoil stagger animation (crisp impact snap -> weighted stagger -> smooth recovery) ---
        private void ApplyHitReactPose(ref FramePose p)
        {
            float hNorm = Mathf.Clamp01(_hitReactTimer / HitReactDuration);
            float hEnv = hNorm < 0.22f
                ? Mathf.SmoothStep(0f, 1f, hNorm / 0.22f)
                : Mathf.SmoothStep(1f, 0f, (hNorm - 0.22f) / 0.78f);
            p.hitStaggerW = hEnv;
            p.targetHipsPos.y -= 0.095f * hEnv;
            p.targetHipsPos.z -= 0.075f * hEnv;
            p.targetHipsRot.y += 10f * hEnv;
            p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, -26f, hEnv);
            p.targetSpineRot.y = Mathf.Lerp(p.targetSpineRot.y, 22f, hEnv);
            p.targetHeadRot.x = Mathf.Lerp(p.targetHeadRot.x, -24f, hEnv);
            if (!p.carrying)
            {
                p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-78f, 0f, -40f), hEnv);
                p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-70f, 0f, 38f), hEnv);
                p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-52f, 0f, 0f), hEnv);
                p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-56f, 0f, 0f), hEnv);
            }
        }

        // --- 3-Hit Weighted Street Combat Combo: 0 = Left Jab, 1 = Heavy Right Hook, 2 = Front Push Kick ---
        private void ApplyCombatComboPose(ref FramePose p)
        {
            float dur = _combatMoveIndex == 2 ? KickDuration : PunchDuration;
            float pNorm = Mathf.Clamp01(_punchTimer / dur);
            // Smooth anticipation wind-up -> explosive snap -> controlled recovery (zero frame pops)
            float wind = pNorm < 0.24f
                ? Mathf.SmoothStep(0f, 1f, pNorm / 0.24f)
                : (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pNorm - 0.24f) / 0.14f)));
            float strike = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pNorm - 0.18f) / 0.24f)) *
                           (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pNorm - 0.54f) / 0.46f)));
            float thrust = strike - wind * 0.42f;
            float tPos = Mathf.Clamp01(strike);
            float guardEnv = Mathf.Sin(pNorm * Mathf.PI);

            if (_combatMoveIndex == 0)
            {
                // Hit 1: Snappy Left Jab with hip-to-shoulder kinetic chain & gaze locked on target
                p.targetHipsPos.y -= 0.042f * guardEnv;
                p.targetHipsPos.z += 0.036f * tPos;
                p.targetHipsRot.y += 14f * thrust;
                p.targetSpineRot.y += 26f * thrust;
                p.targetSpineRot.x += 11f * tPos;
                p.targetHeadRot.y -= 20f * thrust;
                p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-92f, 12f, -6f), tPos);
                p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-6f, 0f, 0f), tPos);
                p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-56f, -20f, 16f), guardEnv);
                p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-98f, 0f, 0f), guardEnv);
            }
            else if (_combatMoveIndex == 1)
            {
                // Hit 2: Heavy Right Hook / Cross with explosive pelvis whip & tight left chin guard
                p.targetHipsPos.y -= 0.062f * guardEnv;
                p.targetHipsPos.z += 0.055f * tPos;
                p.targetHipsRot.y += -22f * thrust;
                p.targetSpineRot.y += -40f * thrust;
                p.targetSpineRot.x += 16f * tPos;
                p.targetSpineRot.z += -6f * tPos;
                p.targetHeadRot.y += 30f * thrust;
                p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(-88f, -38f, 26f), tPos);
                p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-34f, 0f, 0f), tPos);
                p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-58f, 22f, -18f), guardEnv);
                p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-102f, 0f, 0f), guardEnv);
            }
            else
            {
                // Hit 3: Powerful Front Push Kick — continuous chamber -> snap extension -> smooth recovery (zero 80-deg knee pop!)
                float chamberIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pNorm / 0.26f));
                float snapExt = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pNorm - 0.22f) / 0.20f));
                float retract = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pNorm - 0.62f) / 0.38f));
                float kickEnv = chamberIn * (1f - retract);
                float extendPeak = snapExt * (1f - retract);

                p.targetHipsPos.y -= 0.055f * kickEnv;
                p.targetHipsPos.z += 0.045f * extendPeak;
                p.targetHipsRot.x += -10f * extendPeak;
                p.targetHipsRot.y += -12f * extendPeak;
                p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, -22f, extendPeak);
                p.targetSpineRot.y += -14f * extendPeak;
                p.targetHeadRot.x += 14f * extendPeak;
                p.targetHeadRot.y += 14f * extendPeak;

                // Planted left support leg bends athletically to anchor the kick
                p.targetLHipRot = Vector3.Lerp(p.targetLHipRot, new Vector3(-16f, 0f, -6f), kickEnv);
                p.targetLKneeRot = Vector3.Lerp(p.targetLKneeRot, new Vector3(24f, 0f, 0f), kickEnv);
                p.targetLAnkleRot = Vector3.Lerp(p.targetLAnkleRot, new Vector3(-8f, 0f, 0f), kickEnv);

                // Kicking right leg smoothly chambers knee high (92 deg) then whips straight (10 deg)
                float rightHipPitch = Mathf.Lerp(-68f, -104f, snapExt);
                float rightKneeBend = Mathf.Lerp(92f, 10f, snapExt);
                p.targetRHipRot = Vector3.Lerp(p.targetRHipRot, new Vector3(rightHipPitch, 0f, 6f), kickEnv);
                p.targetRKneeRot = Vector3.Lerp(p.targetRKneeRot, new Vector3(rightKneeBend, 0f, 0f), kickEnv);
                p.targetRAnkleRot = Vector3.Lerp(p.targetRAnkleRot, new Vector3(Mathf.Lerp(-12f, 26f, snapExt), 0f, 0f), kickEnv);

                // Counter-balance guard arms
                p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, new Vector3(-48f, 10f, -36f), kickEnv);
                p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, new Vector3(-68f, 0f, 0f), kickEnv);
                p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, new Vector3(28f, 0f, 30f), kickEnv);
                p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, new Vector3(-42f, 0f, 0f), kickEnv);
            }
        }

        // --- Startled / vehicle-dodge pose (step & lean away from threat side with defensive arms) ---
        private void ApplyStartledPose(ref FramePose p)
        {
            float sNorm = Mathf.Clamp01(_startledTimer / _startledDuration);
            float sEnv = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sNorm / 0.14f)) *
                         (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sNorm - 0.52f) / 0.48f)));

            if (_speedMs > 0.35f && Mathf.Abs(p.localTravel.x) > 0.35f)
            {
                _startledDodgeSide = Mathf.Sign(p.localTravel.x);
                _startledHasThreat = true;
            }

            float dodgeSide = _startledDodgeSide;
            float lateralScale = _startledHasThreat ? 1f : 0.45f;
            p.startledStepW = (_grounded && _speedMs < 0.35f) ? (sEnv * lateralScale) : 0f;

            // Lean and shift away from the approaching vehicle while turning gaze toward it
            p.targetHipsPos.x += dodgeSide * 0.13f * sEnv * lateralScale;
            p.targetHipsPos.y -= 0.095f * sEnv;
            p.targetHipsPos.z -= 0.06f * sEnv;
            p.targetHipsRot.z -= dodgeSide * 6f * sEnv * lateralScale;
            p.targetSpineRot.x = Mathf.Lerp(p.targetSpineRot.x, -10f, sEnv);
            p.targetSpineRot.y += dodgeSide * 18f * sEnv * lateralScale;
            p.targetSpineRot.z -= dodgeSide * 17f * sEnv * lateralScale;
            p.targetHeadRot.y -= dodgeSide * 24f * sEnv * lateralScale;
            p.targetHeadRot.x = Mathf.Lerp(p.targetHeadRot.x, -6f, sEnv);

            if (!p.carrying)
            {
                // Near-side arm shields toward threat; far-side arm reaches out toward dodge side for balance
                Vector3 shieldSh = new Vector3(-112f, dodgeSide * 16f, -dodgeSide * 26f);
                Vector3 shieldElb = new Vector3(-68f, 0f, 0f);
                Vector3 balanceSh = new Vector3(-76f, -dodgeSide * 10f, dodgeSide * 48f);
                Vector3 balanceElb = new Vector3(-40f, 0f, 0f);

                if (dodgeSide > 0f)
                {
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, shieldSh, sEnv);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, shieldElb, sEnv);
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, balanceSh, sEnv);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, balanceElb, sEnv);
                }
                else
                {
                    p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, shieldSh, sEnv);
                    p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, shieldElb, sEnv);
                    p.targetLShoulderRot = Vector3.Lerp(p.targetLShoulderRot, balanceSh, sEnv);
                    p.targetLElbowRot = Vector3.Lerp(p.targetLElbowRot, balanceElb, sEnv);
                }
            }
        }

        // --- One-shot right-arm wave overlay (~1.5 s) ---
        private void ApplyWavePose(ref FramePose p)
        {
            float norm = _waveTimer / WaveDuration;
            float env = Mathf.SmoothStep(0f, 1f, norm / 0.24f) *
                (1f - Mathf.SmoothStep(0f, 1f, (norm - 0.73f) / 0.27f));
            float waveOsc = Mathf.Sin(_waveTimer * 15f) * 20f * env;

            Vector3 waveShoulder = new Vector3(-42f, 10f, 118f + waveOsc * 0.22f);
            Vector3 waveElbow = new Vector3(-26f, 0f, 50f + waveOsc);
            p.targetRShoulderRot = Vector3.Lerp(p.targetRShoulderRot, waveShoulder, env);
            p.targetRElbowRot = Vector3.Lerp(p.targetRElbowRot, waveElbow, env);
            p.targetSpineRot.z -= 4f * env;
            p.targetHeadRot.y = Mathf.Lerp(p.targetHeadRot.y, 10f, env * 0.55f);
            p.targetHeadRot.z += 5f * env;
        }
    }
}
