using OurWorld.World;
using UnityEngine;

namespace OurWorld.Player
{
    public partial class ProceduralHumanoid
    {
        private void AnimateSitting(float dt)
        {
            if (_autoSeatActive)
            {
                _autoSeatElapsed += dt;
                float t = Mathf.Clamp01(_autoSeatElapsed / _autoSeatDuration);
                _seatAmount = Mathf.Lerp(_autoSeatFrom, _autoSeatTo, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f)
                {
                    _autoSeatActive = false;
                    if (_autoSeatTo <= 0f)
                    {
                        SetSitting(false);
                        return;
                    }
                }
            }

            float breath = Mathf.Sin(_idleTime * Mathf.PI * 2f * 0.25f) * 0.8f;

            if (isDrivingSeat)
            {
                float steerNorm = Mathf.Clamp(_steerAngleDeg / 36f, -1f, 1f);
                _smoothedDriveSteerNorm = Mathf.Lerp(_smoothedDriveSteerNorm, steerNorm, 1f - Mathf.Exp(-11f * dt));
                float sNorm = _smoothedDriveSteerNorm;
                float accelPitch = Mathf.Clamp(-_driveLongitudinalAccel * 0.75f, -5.5f, 6.5f);

                hips.localPosition = Vector3.Lerp(hips.localPosition, new Vector3(0f, 0.17f, 0f), 1f - Mathf.Exp(-12f * dt));
                SmoothRotate(hips, Vector3.zero, 12f, dt);
                float wheelLean = _steeringWheel != null ? 8f : -6f;
                SmoothRotate(spine, new Vector3(wheelLean + breath + accelPitch, sNorm * 6f, -sNorm * 4f), 10f, dt);
                SmoothRotate(neck, new Vector3(4f - accelPitch * 0.35f, sNorm * 4f, 0f), 10f, dt);
                SmoothRotate(head, new Vector3(-accelPitch * 0.25f, sNorm * 16f, sNorm * 2.5f), 8f, dt);

                SmoothRotate(lHip, new Vector3(-90f, 0f, -6f), 12f, dt);
                SmoothRotate(rHip, new Vector3(-90f, 0f, 6f), 12f, dt);
                SmoothRotate(lKnee, new Vector3(80f, 0f, 0f), 12f, dt);
                SmoothRotate(rKnee, new Vector3(80f - _driveThrottleInput * 4f, 0f, 0f), 12f, dt);
                SmoothRotate(lAnkle, new Vector3(10f, 0f, 0f), 12f, dt);
                SmoothRotate(rAnkle, new Vector3(10f + _driveThrottleInput * 14f, 0f, 0f), 12f, dt);

                float leftReach = -62f - sNorm * 14f;
                float rightReach = -62f + sNorm * 14f;
                SmoothRotate(lShoulder, new Vector3(leftReach, 10f + sNorm * 10f, -10f - sNorm * 8f), 14f, dt);
                SmoothRotate(rShoulder, new Vector3(rightReach, -10f + sNorm * 10f, 10f - sNorm * 8f), 14f, dt);
                SmoothRotate(lElbow, new Vector3(-32f + sNorm * 10f, 0f, 0f), 14f, dt);
                SmoothRotate(rElbow, new Vector3(-32f - sNorm * 10f, 0f, 0f), 14f, dt);
                if (_steeringWheel != null)
                {
                    // Project driver basis onto steering wheel plane (invariant to wheel local-Y spin) so hands stay cleanly on the rim without ever tangling at full lock
                    Vector3 wheelAxis = _steeringWheel.up.normalized;
                    Vector3 wheelRight = Vector3.ProjectOnPlane(transform.right, wheelAxis);
                    Vector3 wheelUp = Vector3.ProjectOnPlane(transform.up, wheelAxis);
                    if (wheelRight.sqrMagnitude > 0.001f && wheelUp.sqrMagnitude > 0.001f)
                    {
                        wheelRight.Normalize();
                        wheelUp.Normalize();
                        const float rimRadius = 0.162f;
                        float handTurnRad = Mathf.Clamp(sNorm * 68f, -68f, 68f) * Mathf.Deg2Rad;
                        float rightAngle = 14f * Mathf.Deg2Rad - handTurnRad;
                        float leftAngle = 166f * Mathf.Deg2Rad - handTurnRad;
                        Vector3 rimCenter = _steeringWheel.position - wheelAxis * 0.024f;
                        Vector3 leftTarget = rimCenter + (wheelRight * Mathf.Cos(leftAngle) + wheelUp * Mathf.Sin(leftAngle)) * rimRadius;
                        Vector3 rightTarget = rimCenter + (wheelRight * Mathf.Cos(rightAngle) + wheelUp * Mathf.Sin(rightAngle)) * rimRadius;
                        AimHand(lShoulder, lElbow, leftTarget, -transform.right - transform.up * 0.25f, 1f);
                        AimHand(rShoulder, rElbow, rightTarget, transform.right - transform.up * 0.25f, 1f);
                    }
                    else
                    {
                        AimHand(lShoulder, lElbow, _steeringWheel.TransformPoint(new Vector3(-0.145f, 0f, 0.055f)), -transform.right, 1f);
                        AimHand(rShoulder, rElbow, _steeringWheel.TransformPoint(new Vector3(0.145f, 0f, 0.055f)), transform.right, 1f);
                    }
                }
                return;
            }

            // Non-driving seated life: talking decay/blend + chest breathing (desynchronized via _noiseSeed)
            if (_talkingTimer > 0f)
            {
                _talkingTimer = Mathf.Max(0f, _talkingTimer - dt);
            }
            _talkingBlend = Mathf.Lerp(_talkingBlend, _talkingTimer > 0f ? 1f : 0f, 1f - Mathf.Exp(-6f * dt));

            float seatedBreathWave = Mathf.Sin((_idleTime + _noiseSeed) * TwoPi * 0.25f);
            float seatedBreath = seatedBreathWave * 0.8f;
            if (chestVisual != null)
            {
                float breathScale = 1f + seatedBreathWave * 0.018f;
                Vector3 targetScale = new Vector3(
                    _chestBaseScale.x * breathScale,
                    _chestBaseScale.y,
                    _chestBaseScale.z * breathScale);
                chestVisual.localScale = Vector3.Lerp(chestVisual.localScale, targetScale, 1f - Mathf.Exp(-6f * dt));
            }

            if (_seatFloor.HasValue)
            {
                AnimateSeatContact(_seatFloor.Value, _seatAmount, seatedBreath, dt);
            }
            else
            {
                float headNoise = (Mathf.PerlinNoise(_noiseSeed, _idleTime * 0.16f) - 0.5f) * 2f;
                float headLookYaw = headNoise * 11f * (_hasLookPoint ? 0.15f : 1f);

                float gestureW = UpdateSeatedGesture(dt, lookAroundOnly: true);
                float gestureT = _seatGestureDuration > 0.01f ? Mathf.Clamp01(_seatGestureTimer / _seatGestureDuration) : 0f;

                Vector3 spineRot = new Vector3(-7f + seatedBreath, 0f, 0f);
                Vector3 neckRot = new Vector3(5f, 0f, 0f);
                Vector3 headRot = new Vector3(0f, headLookYaw, 0f);

                if (gestureW > 0.001f && _seatGestureKind == 4)
                {
                    float scan = Mathf.Sin(gestureT * TwoPi);
                    headRot.y += scan * 38f * gestureW;
                    spineRot.y += scan * 6f * gestureW;
                }

                Vector3 lShRot = new Vector3(8f + seatedBreath * 0.5f, 0f, -10f);
                Vector3 rShRot = new Vector3(8f + seatedBreath * 0.5f, 0f, 10f);
                Vector3 lElbRot = new Vector3(-44f, 0f, 10f);
                Vector3 rElbRot = new Vector3(-44f, 0f, -10f);

                if (_talkingBlend > 0.02f)
                {
                    float talkTime = _idleTime + _noiseSeed;
                    float beatR = Mathf.Sin(talkTime * 6.2f);
                    float beatL = Mathf.Sin(talkTime * 6.2f + 1.5f);
                    headRot.x += Mathf.Sin(talkTime * 4.1f) * 4f * _talkingBlend;
                    spineRot.x += 6f * _talkingBlend;
                    rShRot = Vector3.Lerp(rShRot, new Vector3(-34f - beatR * 12f, -10f, 14f), _talkingBlend);
                    rElbRot = Vector3.Lerp(rElbRot, new Vector3(-74f + beatR * 16f, 0f, -10f), _talkingBlend);
                    lShRot = Vector3.Lerp(lShRot, new Vector3(-16f - beatL * 6f, 6f, -12f), _talkingBlend * 0.65f);
                    lElbRot = Vector3.Lerp(lElbRot, new Vector3(-58f + beatL * 8f, 0f, 10f), _talkingBlend * 0.65f);
                }

                ApplySeatedLookAt(dt, ref spineRot, ref neckRot, ref headRot);

                hips.localPosition = Vector3.Lerp(hips.localPosition, new Vector3(0f, 0.08f, 0.02f), 1f - Mathf.Exp(-12f * dt));
                SmoothRotate(hips, Vector3.zero, 12f, dt);
                SmoothRotate(spine, spineRot, 10f, dt);
                SmoothRotate(neck, neckRot, 10f, dt);
                SmoothRotate(head, headRot, 6f, dt);

                SmoothRotate(lHip, new Vector3(-88f, 0f, -5f), 12f, dt);
                SmoothRotate(rHip, new Vector3(-88f, 0f, 5f), 12f, dt);
                SmoothRotate(lKnee, new Vector3(88f, 0f, 0f), 12f, dt);
                SmoothRotate(rKnee, new Vector3(88f, 0f, 0f), 12f, dt);
                SmoothRotate(lAnkle, Vector3.zero, 12f, dt);
                SmoothRotate(rAnkle, Vector3.zero, 12f, dt);

                // Hands resting comfortably downward on lap/thighs (never floating out in front!)
                SmoothRotate(lShoulder, lShRot, 12f, dt);
                SmoothRotate(rShoulder, rShRot, 12f, dt);
                SmoothRotate(lElbow, lElbRot, 12f, dt);
                SmoothRotate(rElbow, rElbRot, 12f, dt);
            }
        }

        private float UpdateSeatedGesture(float dt, bool lookAroundOnly = false)
        {
            if (_seatGestureCooldown < 0f)
            {
                _seatGestureCooldown = UnityEngine.Random.Range(2f, 8f);
            }

            if (_seatGestureTimer >= 0f)
            {
                _seatGestureTimer += dt;
                if (_seatGestureTimer >= _seatGestureDuration)
                {
                    _seatGestureTimer = -1f;
                    _seatGestureKind = 0;
                    _seatGestureCooldown = UnityEngine.Random.Range(5f, 13f);
                    return 0f;
                }

                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_seatGestureTimer / 0.6f));
                float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_seatGestureDuration - _seatGestureTimer) / 0.6f));
                float w = Mathf.Min(fadeIn, fadeOut) * (1f - _talkingBlend);

                if (_talkingTimer > 0f && _talkingBlend > 0.96f)
                {
                    _seatGestureTimer = -1f;
                    _seatGestureKind = 0;
                    _seatGestureCooldown = UnityEngine.Random.Range(5f, 13f);
                    return 0f;
                }
                return w;
            }

            if (_talkingTimer > 0f) return 0f;

            _seatGestureCooldown -= dt;
            if (_seatGestureCooldown <= 0f)
            {
                _seatGestureKind = lookAroundOnly ? 4 : UnityEngine.Random.Range(1, 6);
                _seatGestureDuration = _seatGestureKind == 1
                    ? UnityEngine.Random.Range(6f, 12f)
                    : UnityEngine.Random.Range(2.6f, 4.8f);
                _seatGestureTimer = 0f;
            }
            return 0f;
        }

        private void ApplySeatedLookAt(float dt, ref Vector3 spineRot, ref Vector3 neckRot, ref Vector3 headRot)
        {
            float targetYaw = 0f;
            float targetPitch = 0f;
            if (_hasLookPoint)
            {
                Vector3 chestWorld = spine != null
                    ? spine.position + transform.up * (0.30f * transform.lossyScale.y)
                    : transform.TransformPoint(new Vector3(0f, 0.56f, 0f));
                Vector3 localDelta = transform.InverseTransformDirection(_lookWorldPoint - chestWorld);
                float horizDist = new Vector2(localDelta.x, localDelta.z).magnitude;
                if (horizDist > 0.10f)
                {
                    float rawYaw = Mathf.Atan2(localDelta.x, localDelta.z) * Mathf.Rad2Deg;
                    targetYaw = Mathf.Clamp(rawYaw, -70f, 70f);
                    targetPitch = Mathf.Clamp(-Mathf.Atan2(localDelta.y, horizDist) * Mathf.Rad2Deg, -25f, 25f);
                }
            }

            float blend = 1f - Mathf.Exp(-5f * dt);
            _smoothedLookYaw = Mathf.Lerp(_smoothedLookYaw, targetYaw, blend);
            _smoothedLookPitch = Mathf.Lerp(_smoothedLookPitch, targetPitch, blend);

            headRot.y += _smoothedLookYaw * 0.70f;
            headRot.x += _smoothedLookPitch * 0.70f;
            neckRot.y += _smoothedLookYaw * 0.20f;
            neckRot.x += _smoothedLookPitch * 0.30f;
            spineRot.y += _smoothedLookYaw * 0.10f;
        }

        private void AnimateSeatContact(float floor, float seated, float breath, float dt)
        {
            float shift = Mathf.Sin(seated * Mathf.PI);

            float gestureW = 0f;
            int gestureKind = 0;
            float gestureT = 0f;
            if (seated >= 0.98f && !_autoSeatActive)
            {
                gestureW = UpdateSeatedGesture(dt, lookAroundOnly: false);
                gestureKind = _seatGestureKind;
                gestureT = _seatGestureDuration > 0.01f ? Mathf.Clamp01(_seatGestureTimer / _seatGestureDuration) : 0f;
            }
            else if (_seatGestureTimer >= 0f)
            {
                _seatGestureTimer = -1f;
                _seatGestureKind = 0;
            }

            Vector3 spineRot = new Vector3(30f * shift - 5f * seated + breath, 0f, 0f);
            Vector3 neckRot = new Vector3(-8f * shift + 5f * seated, 0f, 0f);
            Vector3 headRot = new Vector3(12f * shift, 0f, 0f);

            if (gestureW > 0.001f)
            {
                if (gestureKind == 1)
                {
                    spineRot.x -= 5f * gestureW;
                }
                else if (gestureKind == 2)
                {
                    spineRot.x -= 11f * gestureW;
                    neckRot.x += 6f * gestureW;
                }
                else if (gestureKind == 3)
                {
                    spineRot.x += 24f * gestureW;
                    neckRot.x -= 8f * gestureW;
                    headRot.x -= 8f * gestureW;
                }
                else if (gestureKind == 4)
                {
                    float scan = Mathf.Sin(gestureT * TwoPi);
                    headRot.y += scan * 38f * gestureW;
                    spineRot.y += scan * 6f * gestureW;
                }
                else if (gestureKind == 5)
                {
                    spineRot.x += 12f * gestureW;
                }
            }

            if (_talkingBlend > 0.02f)
            {
                float talkTime = _idleTime + _noiseSeed;
                headRot.x += Mathf.Sin(talkTime * 4.1f) * 4f * _talkingBlend;
                spineRot.x += 6f * _talkingBlend;
            }

            ApplySeatedLookAt(dt, ref spineRot, ref neckRot, ref headRot);

            hips.localPosition = new Vector3(0f, Mathf.Lerp(hipsBaseY, 0.08f, seated), 0.09f * shift);
            SmoothRotate(hips, Vector3.zero, 15f, dt);
            SmoothRotate(spine, spineRot, 16f, dt);
            SmoothRotate(neck, neckRot, 14f, dt);
            SmoothRotate(head, headRot, 10f, dt);

            float scaleY = transform.lossyScale.y;
            float footSlide = (gestureKind == 2 && gestureW > 0.001f) ? 0.14f * gestureW : 0f;
            Vector3 left = transform.TransformPoint(new Vector3(-0.14f, 0f, Mathf.Lerp(0.04f, 0.46f, seated) + footSlide));
            Vector3 right = transform.TransformPoint(new Vector3(0.14f, 0f, Mathf.Lerp(0.09f, 0.44f, seated) + footSlide));
            left.y = right.y = floor + AnkleHeight * scaleY;

            if (gestureKind == 1 && gestureW > 0.001f)
            {
                Vector3 crossedRight = transform.TransformPoint(new Vector3(-0.06f, 0f, 0.50f));
                crossedRight.y = floor + AnkleHeight * scaleY + 0.30f * scaleY;
                right = Vector3.Lerp(right, crossedRight, gestureW);
            }

            if (lHip != null && rHip != null && lKnee != null && rKnee != null && lAnkle != null && rAnkle != null)
            {
                float drop = Mathf.Max(0f, Mathf.Max(LegExtension(lHip, lKnee, lAnkle, left),
                    LegExtension(rHip, rKnee, rAnkle, right)));
                hips.position -= transform.up * drop;
                AimLimb(lHip, lKnee, left, transform.forward, lAnkle.localPosition.magnitude);
                AimLimb(rHip, rKnee, right, transform.forward, rAnkle.localPosition.magnitude);
                lAnkle.rotation = transform.rotation;
                rAnkle.rotation = gestureKind == 1 && gestureW > 0.001f
                    ? transform.rotation * Quaternion.Euler(14f * gestureW, -18f * gestureW, 0f)
                    : transform.rotation;
            }

            if (lKnee != null && rKnee != null)
            {
                Vector3 lRest = Vector3.Lerp(transform.TransformPoint(new Vector3(-0.27f, 0.82f, 0.06f)),
                    lKnee.position + transform.up * 0.10f, seated);
                Vector3 rRest = Vector3.Lerp(transform.TransformPoint(new Vector3(0.27f, 0.82f, 0.06f)),
                    rKnee.position + transform.up * 0.10f, seated);

                // Reach back to seat edge to support body weight during sit-down / stand-up transition
                float edgeSupport = Mathf.Clamp01(shift * 1.3f);
                if (edgeSupport > 0.001f)
                {
                    lRest = Vector3.Lerp(lRest, transform.TransformPoint(new Vector3(-0.25f, 0.04f, -0.04f)), edgeSupport);
                    rRest = Vector3.Lerp(rRest, transform.TransformPoint(new Vector3(0.25f, 0.04f, -0.04f)), edgeSupport);
                }

                if (gestureW > 0.001f)
                {
                    if (gestureKind == 1)
                    {
                        lRest = Vector3.Lerp(lRest, rKnee.position + transform.up * 0.08f, gestureW);
                    }
                    else if (gestureKind == 2)
                    {
                        lRest = Vector3.Lerp(lRest, transform.TransformPoint(new Vector3(-0.24f, 0.10f, 0.06f)), gestureW);
                        rRest = Vector3.Lerp(rRest, transform.TransformPoint(new Vector3(0.24f, 0.10f, 0.06f)), gestureW);
                    }
                    else if (gestureKind == 3)
                    {
                        Vector3 midKnees = (lKnee.position + rKnee.position) * 0.5f + transform.forward * 0.10f + transform.up * 0.16f;
                        lRest = Vector3.Lerp(lRest, midKnees - transform.right * 0.04f, gestureW);
                        rRest = Vector3.Lerp(rRest, midKnees + transform.right * 0.04f, gestureW);
                    }
                    else if (gestureKind == 5 && head != null)
                    {
                        Vector3 chin = head.position + transform.forward * 0.11f - transform.up * 0.11f;
                        rRest = Vector3.Lerp(rRest, chin, gestureW);
                    }
                }

                if (_talkingBlend > 0.02f)
                {
                    float talkTime = _idleTime + _noiseSeed;
                    Vector3 talkR = rRest + transform.up * (0.22f + 0.06f * Mathf.Sin(talkTime * 6.2f)) + transform.forward * 0.16f;
                    Vector3 talkL = lRest + transform.up * (0.11f + 0.03f * Mathf.Sin(talkTime * 6.2f + 1.5f)) + transform.forward * 0.08f;
                    rRest = Vector3.Lerp(rRest, talkR, _talkingBlend);
                    lRest = Vector3.Lerp(lRest, talkL, _talkingBlend);
                }

                AimHand(lShoulder, lElbow, lRest, -transform.right, 1f);
                AimHand(rShoulder, rElbow, rRest, transform.right, 1f);
            }
        }

        private Vector3 SwimHand(float side, float phase)
        {
            float stroke = Mathf.Repeat(phase / TwoPi, 1f);
            const float pullEnd = 0.62f;
            float pull = Mathf.Clamp01(stroke / pullEnd);
            float recovery = Mathf.InverseLerp(pullEnd, 1f, stroke);
            float forward = stroke < pullEnd ? Mathf.Lerp(1.04f, -0.07f, pull) : Mathf.SmoothStep(-0.07f, 1.04f, recovery);
            float height = 0.83f - 0.30f * Mathf.Sin(pull * Mathf.PI) + 0.32f * Mathf.Sin(recovery * Mathf.PI);
            float width = 0.22f + 0.15f * Mathf.Sin(recovery * Mathf.PI);
            return transform.TransformPoint(new Vector3(side * width, height, forward));
        }

        public void SetCycling(bool active)
        {
            _cycling = active;
            _motorcyclePose = false;
            if (!active)
            {
                _cycleMount = 1f;
                _cycleFootPeg = 1f;
                _cycleSteerNorm = 0f;
                _cycleAccelLean = 0f;
            }
        }

        public void SetCycling(bool active, Vector3 leftPedal, Vector3 rightPedal, Vector3 leftGrip, Vector3 rightGrip,
            float mount = 1f, float footPeg = 1f, float steerNorm = 0f, float accelLean = 0f)
        {
            _cycling = active;
            _motorcyclePose = false;
            _leftPedal = leftPedal;
            _rightPedal = rightPedal;
            _leftGrip = leftGrip;
            _rightGrip = rightGrip;
            _cycleMount = Mathf.Clamp01(mount);
            _cycleFootPeg = Mathf.Clamp01(footPeg);
            _cycleSteerNorm = Mathf.Clamp(steerNorm, -1f, 1f);
            _cycleAccelLean = Mathf.Clamp(accelLean, -1f, 1f);
        }

        public void SetMotorcyclePose(Vector3 leftPeg, Vector3 rightPeg, Vector3 leftGrip, Vector3 rightGrip,
            float mount, float footPeg, float steerNorm = 0f, float accelLean = 0f)
        {
            SetCycling(true, leftPeg, rightPeg, leftGrip, rightGrip, mount, footPeg, steerNorm, accelLean);
            _motorcyclePose = true;
        }

        public void SetTennisRacket(Transform racket)
        {
            _tennisRacket = rElbow != null ? racket : null;
            if (_tennisRacket != null) _tennisRacket.SetParent(rElbow, false);
            ClearTennisSwing();
            _tennisLeftHand = null;
        }

        public void SetTennisLeftHand(Vector3? point)
        {
            _tennisLeftHand = point;
        }

        public void PlayTennisSwing(Vector3 contact, float secondsToContact, bool serving = false)
        {
            _tennisContact = contact;
            _tennisContactAt = Time.time + secondsToContact;
            _tennisServing = serving;
            _tennisBackhand = !serving && transform.InverseTransformPoint(contact).x < -0.05f;
        }

        public void ClearTennisSwing()
        {
            _tennisContactAt = float.NegativeInfinity;
            _tennisServing = false;
            _tennisBackhand = false;
        }

        public void SetShelfReach(bool active, Vector3 worldPos, float weight)
        {
            _hasShelfReach = active;
            _shelfReachWorldPos = worldPos;
            _shelfReachWeight = active ? Mathf.Clamp01(weight) : 0f;
        }

        public void SetConsumePose(int mode, float weight, float cyclePhase)
        {
            _consumeMode = mode;
            _consumeWeight = mode > 0 ? Mathf.Clamp01(weight) : 0f;
            _consumeCyclePhase = Mathf.Clamp01(cyclePhase);
        }

        private void ApplyTennisRacketPose(float swingTime)
        {
            Vector3 readyHand = transform.TransformPoint(new Vector3(0.24f, 1.06f, 0.30f));
            Vector3 readyAxis = new Vector3(0.42f, 0.86f, 0.28f);
            Vector3 axis = readyAxis;
            Vector3 hand = readyHand;
            Vector3 balanceLeft = transform.TransformPoint(new Vector3(-0.27f, 1.04f, 0.30f));

            if (swingTime >= -TennisWindup && swingTime < TennisFollowThrough)
            {
                Vector3 strikeAxis = _tennisServing
                    ? new Vector3(0.32f, 0.92f, 0.22f)
                    : (_tennisBackhand ? new Vector3(-0.88f, 0.28f, 0.38f) : new Vector3(0.89f, 0.25f, 0.38f));
                Vector3 strikeUp = transform.TransformDirection(strikeAxis.normalized);
                Vector3 strikeFace = Vector3.ProjectOnPlane(transform.forward, strikeUp).normalized;
                Vector3 strikeHand = _tennisContact - strikeFace * TennisCourt.BallRadius -
                    strikeUp * (RacketHeadFromHand * transform.lossyScale.y);

                if (swingTime < 0f)
                {
                    const float backswingTime = -0.12f;
                    Vector3 backswingHand = transform.TransformPoint(_tennisServing
                        ? new Vector3(0.30f, 1.56f, -0.18f)
                        : (_tennisBackhand ? new Vector3(-0.34f, 0.96f, -0.16f) : new Vector3(0.46f, 1.02f, -0.26f)));
                    Vector3 backswingAxis = _tennisServing
                        ? new Vector3(0.42f, 0.32f, -0.85f)
                        : (_tennisBackhand ? new Vector3(-0.72f, 0.42f, -0.55f) : new Vector3(0.66f, 0.45f, -0.60f));
                    if (swingTime < backswingTime)
                    {
                        float windup = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-TennisWindup, backswingTime, swingTime));
                        hand = Vector3.Lerp(readyHand, backswingHand, windup);
                        axis = Vector3.Slerp(readyAxis, backswingAxis, windup);
                        balanceLeft = transform.TransformPoint(Vector3.Lerp(
                            new Vector3(-0.27f, 1.04f, 0.30f),
                            _tennisBackhand ? new Vector3(-0.32f, 1.08f, 0.08f) : new Vector3(-0.42f, 1.16f, 0.42f),
                            windup));
                    }
                    else
                    {
                        float strike = Mathf.InverseLerp(backswingTime, 0f, swingTime);
                        float ease = strike * strike;
                        hand = Vector3.Lerp(backswingHand, strikeHand, ease);
                        axis = Vector3.Slerp(backswingAxis, strikeAxis, ease);
                        Vector3 backBalance = _tennisBackhand
                            ? new Vector3(-0.32f, 1.08f, 0.08f)
                            : new Vector3(-0.42f, 1.16f, 0.42f);
                        balanceLeft = transform.TransformPoint(Vector3.Lerp(
                            backBalance,
                            new Vector3(-0.44f, 1.05f, 0.04f),
                            ease));
                    }
                }
                else
                {
                    Vector3 finishHand = transform.TransformPoint(_tennisServing
                        ? new Vector3(-0.22f, 0.88f, 0.36f)
                        : (_tennisBackhand ? new Vector3(0.38f, 1.32f, 0.34f) : new Vector3(-0.18f, 1.32f, 0.34f)));
                    Vector3 finishAxis = _tennisServing
                        ? new Vector3(-0.62f, -0.58f, 0.52f)
                        : (_tennisBackhand ? new Vector3(0.78f, 0.58f, 0.24f) : new Vector3(-0.78f, 0.60f, 0.22f));
                    const float finishTime = 0.14f;
                    if (swingTime < finishTime)
                    {
                        float follow = Mathf.SmoothStep(0f, 1f, swingTime / finishTime);
                        hand = Vector3.Lerp(strikeHand, finishHand, follow);
                        axis = Vector3.Slerp(strikeAxis, finishAxis, follow);
                        balanceLeft = transform.TransformPoint(Vector3.Lerp(
                            new Vector3(-0.44f, 1.05f, 0.04f),
                            new Vector3(-0.44f, 1.06f, -0.06f),
                            follow));
                    }
                    else
                    {
                        float recover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(finishTime, TennisFollowThrough, swingTime));
                        hand = Vector3.Lerp(finishHand, readyHand, recover);
                        axis = Vector3.Slerp(finishAxis, readyAxis, recover);
                        balanceLeft = transform.TransformPoint(Vector3.Lerp(new Vector3(-0.44f, 1.06f, -0.06f), new Vector3(-0.27f, 1.04f, 0.30f), recover));
                    }
                }
            }

            AimHand(rShoulder, rElbow, hand, _tennisBackhand ? -transform.right : transform.right, 1f);
            AimHand(lShoulder, lElbow, _tennisLeftHand ?? balanceLeft, -transform.right, 1f);
            Vector3 racketUp = transform.TransformDirection(axis.normalized);
            Vector3 racketForward = Vector3.ProjectOnPlane(transform.forward, racketUp).normalized;
            Quaternion rotation = Quaternion.LookRotation(racketForward, racketUp);
            Vector3 handCenter = rElbow.TransformPoint(Vector3.down * HandLength);
            _tennisRacket.SetPositionAndRotation(handCenter - racketUp * (RacketHandleCenter * transform.lossyScale.y), rotation);
        }

        private void AimHand(Transform shoulder, Transform elbow, Vector3 target, Vector3 outward, float weight)
        {
            if (shoulder == null || elbow == null) return;
            Quaternion shoulderPose = shoulder.localRotation;
            Quaternion elbowPose = elbow.localRotation;
            AimLimb(shoulder, elbow, target, -transform.up + outward * 0.55f, HandLength);
            shoulder.localRotation = Quaternion.Slerp(shoulderPose, shoulder.localRotation, weight);
            elbow.localRotation = Quaternion.Slerp(elbowPose, elbow.localRotation, weight);
        }

        private void AnimateCycling(float dt)
        {
            float mount = _cycleMount;
            float footPeg = _cycleFootPeg;
            _cycleSmoothedSteer = Mathf.Lerp(_cycleSmoothedSteer, _cycleSteerNorm, 1f - Mathf.Exp(-10f * dt));
            _cycleSmoothedAccel = Mathf.Lerp(_cycleSmoothedAccel, _cycleAccelLean, 1f - Mathf.Exp(-8f * dt));
            float breathWave = Mathf.Sin((_idleTime + _noiseSeed) * TwoPi * 0.28f);
            float breath = breathWave * 0.7f;

            Vector3 seatedHips = _motorcyclePose
                ? new Vector3(Mathf.Lerp(-0.11f, 0f, footPeg), Mathf.Lerp(0.91f, 0.97f, footPeg), -0.25f)
                : new Vector3(Mathf.Lerp(-0.07f, 0f, footPeg), Mathf.Lerp(hipsBaseY + 0.04f, hipsBaseY + 0.115f, footPeg), -0.235f);
            Vector3 targetHipsPos = Vector3.Lerp(new Vector3(0f, hipsBaseY, 0f), seatedHips, mount);
            hips.localPosition = Vector3.Lerp(hips.localPosition, targetHipsPos, 1f - Mathf.Exp(-18f * dt));
            SmoothRotate(hips, new Vector3(0f, 0f, -_cycleSmoothedSteer * (_motorcyclePose ? 4f : 3f) * mount), 14f, dt);

            float lean = ((_motorcyclePose ? 19f : 31f) + _cycleSmoothedAccel * (_motorcyclePose ? 8.5f : 5.5f) + breath) * mount;
            SmoothRotate(spine, new Vector3(lean, _cycleSmoothedSteer * 7f * mount, -_cycleSmoothedSteer * 4.5f * mount), 12f, dt);
            SmoothRotate(neck, new Vector3(-lean * 0.65f, _cycleSmoothedSteer * 5f * mount, 0f), 12f, dt);
            SmoothRotate(head, new Vector3((-7f - _cycleSmoothedAccel * 2.5f) * mount, _cycleSmoothedSteer * 18f * mount, _cycleSmoothedSteer * 4f * mount), 10f, dt);

            if (chestVisual != null)
            {
                float breathScale = 1f + breathWave * 0.015f;
                Vector3 targetScale = new Vector3(_chestBaseScale.x * breathScale, _chestBaseScale.y, _chestBaseScale.z * breathScale);
                chestVisual.localScale = Vector3.Lerp(chestVisual.localScale, targetScale, 1f - Mathf.Exp(-6f * dt));
            }

            Vector3 ankleOffset = transform.up * 0.065f - transform.forward * 0.04f;
            Vector3 leftFoot = Vector3.Lerp(transform.TransformPoint(new Vector3(-0.13f, AnkleHeight, 0f)),
                _leftPedal + ankleOffset, mount);
            Vector3 rightFoot = Vector3.Lerp(transform.TransformPoint(new Vector3(0.13f,
                AnkleHeight + Mathf.Sin(mount * Mathf.PI) * 0.34f, 0f)), _rightPedal + ankleOffset, mount);
            AimLimb(lHip, lKnee, leftFoot, transform.forward, 0.42f);
            AimLimb(rHip, rKnee, rightFoot, transform.forward, 0.42f);
            if (lAnkle != null) lAnkle.rotation = transform.rotation * Quaternion.Euler(Mathf.Lerp(0f, 6f, footPeg * mount), 0f, 0f);
            if (rAnkle != null) rAnkle.rotation = transform.rotation * Quaternion.Euler(6f * mount, 0f, 0f);
            Vector3 leftHand = Vector3.Lerp(transform.TransformPoint(new Vector3(-0.26f, 0.83f, 0.08f)), _leftGrip, mount);
            Vector3 rightHand = Vector3.Lerp(transform.TransformPoint(new Vector3(0.26f, 0.83f, 0.08f)), _rightGrip, mount);
            AimHand(lShoulder, lElbow, leftHand, -transform.right, 1f);
            AimHand(rShoulder, rElbow, rightHand, transform.right, 1f);
        }

        private void AimLimb(Transform first, Transform second, Vector3 target, Vector3 bendToward, float lowerLength)
        {
            if (first == null || second == null) return;

            Vector3 start = first.position;
            Vector3 delta = target - start;
            Vector3 direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : -transform.up;
            float upperLength = second.localPosition.magnitude * transform.lossyScale.y;
            lowerLength *= transform.lossyScale.y;
            float distance = Mathf.Clamp(delta.magnitude,
                Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 pole = Vector3.ProjectOnPlane(bendToward, direction).normalized;
            if (pole.sqrMagnitude < 0.0001f) pole = Vector3.ProjectOnPlane(transform.right, direction).normalized;
            Vector3 elbowOrKnee = start + direction * along + pole * bend;
            AimBone(first, elbowOrKnee - start);
            AimBone(second, start + direction * distance - second.position);
        }

        private void AimBone(Transform bone, Vector3 direction)
        {
            Vector3 forward = Vector3.Cross(direction, transform.right);
            if (forward.sqrMagnitude < 0.000001f) forward = Vector3.ProjectOnPlane(transform.forward, direction);
            bone.rotation = Quaternion.LookRotation(forward, -direction);
        }

        private static void SmoothRotate(Transform target, Vector3 targetEuler, float damp, float dt)
        {
            if (target == null) return;
            target.localRotation = Quaternion.Slerp(
                target.localRotation,
                Quaternion.Euler(targetEuler),
                1f - Mathf.Exp(-damp * dt));
        }
    }
}
