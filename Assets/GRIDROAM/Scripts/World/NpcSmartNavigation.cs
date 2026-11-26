using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;

namespace OurWorld.World
{
    public struct ApproachingVehicleInfo
    {
        public bool Detected;
        public bool Urgent;
        public Vector3 VehiclePos;
        public Vector3 VehicleForward;
        public Vector3 VehicleRight;
        public float Distance;
        public float LateralOffset;
        public float SpeedKmh;
    }

    /// <summary>
    /// Shared zero-allocation intelligent navigation, multi-whisker dual-height obstacle avoidance,
    /// right-hand sidewalk passing, traffic/bicycle detection, rain shelter seeking, and
    /// player-context awareness library powering ALL Hamidiye Mahallesi NPCs & wildlife.
    /// </summary>
    public static class NpcSmartNavigation
    {
        private static readonly RaycastHit[] SweepHits = new RaycastHit[12];
        private static readonly float[] WhiskerAngles = { 0f, 28f, -28f, 58f, -58f };

        // Authentic covered awnings, canopies, gazebos, porches & shelters across Hamidiye Mahallesi
        private static readonly Vector3[] RainShelterSpots =
        {
            // Selman Sokağı shop awnings & café porches
            new Vector3(-32.5f, 0.06f,   5.5f),
            new Vector3(-16.5f, 0.06f,   5.3f),
            new Vector3(  2.6f, 0.06f,   4.6f),
            new Vector3( 22.9f, 0.06f,   5.1f),
            new Vector3(-31.2f, 0.06f,  -5.5f),
            new Vector3( 44.0f, 0.06f,  -5.9f),
            // Town Square & Park tea garden umbrellas / pergola
            new Vector3( 14.5f, 0.06f,  15.2f),
            new Vector3( 18.6f, 0.06f,  15.5f),
            new Vector3(-18.0f, 0.06f,  69.0f),
            // Mosque courtyard portico
            new Vector3( 56.0f, 0.06f, -18.5f),
            // Sports Complex covered Halı Saha tribün & tea deck
            new Vector3( -2.5f, 0.06f, -23.5f),
            new Vector3(  2.2f, 0.06f, -23.5f),
            // Hamidiye Atlas AVM south entrance canopy
            new Vector3(-116.0f, 0.08f, -52.5f),
            // Hamidiye Oto Galeri covered showroom forecourt
            new Vector3(-115.0f, 0.14f,  68.5f),
            // Sultanbeyli Lake wooden gazebos
            new Vector3(-168.0f, 0.22f,  15.0f),
            new Vector3(-168.0f, 0.22f, -15.0f),
            // Havadis Sokağı stone bakery porch & Kadife Kıraathane porch
            new Vector3(-22.0f, 0.06f, 181.2f),
            new Vector3(-16.0f, 0.06f, -181.2f),
            // Aydos Farm barn & shelter
            new Vector3(168.0f, 0.06f,   5.5f)
        };

        // Window-shopping, bakery, fountain, coffee & stall interest points for realistic daily micro-stops
        private static readonly (Vector3 pos, Vector3 faceTarget, string trLine, string enLine)[] DailyInterestSpots =
        {
            (new Vector3(  6.8f, 0.06f,  11.8f), new Vector3(  6.8f, 0.8f,  13.8f), "Çıtır simit kokusu buradan geliyor!", "Fresh sesame simit smells amazing!"),
            (new Vector3( -9.5f, 0.06f,   6.2f), new Vector3( -9.5f, 0.8f,   8.2f), "Bir sıcak simit alayım bari.", "Let me grab a warm simit."),
            (new Vector3(  6.5f, 0.06f,   4.8f), new Vector3(  6.5f, 1.2f,   7.5f), "Selman Bakkal'da taze meyve gelmiş!", "Fresh fruit just arrived at Selman Market!"),
            (new Vector3( 47.5f, 0.06f, -17.2f), new Vector3( 47.5f, 1.2f, -15.0f), "Mahalle çeşmesinin suyu buz gibi.", "Water from the neighborhood fountain is ice cold."),
            (new Vector3( -8.5f, 0.06f, -20.3f), new Vector3( -8.5f, 1.2f, -22.5f), "Spordan sonra çeşmeden serinleyelim.", "Nothing beats cool fountain water after sports."),
            (new Vector3(  0.0f, 0.16f, 134.5f), new Vector3(  0.0f, 4.0f, 142.0f), "Saat Kulesi meydanı bugün pek canlı.", "Clock Tower Plaza is bustling today."),
            (new Vector3(-24.0f, 0.06f, 181.5f), new Vector3(-24.0f, 1.2f, 184.5f), "Taş fırından yeni börek çıkmış!", "Fresh pastry just came out of the stone oven!"),
            (new Vector3(-16.0f, 0.06f, -181.5f), new Vector3(-16.0f, 1.2f, -184.5f), "Kıraathanede çaylar taze demlenmiş.", "Fresh tea is brewing at the tea house."),
            (new Vector3(-116.0f, 0.08f, -54.0f), new Vector3(-116.0f, 2.5f, -45.0f), "Atlas AVM'de bugün indirim varmış!", "Heard there's a big sale at Atlas Mall today!"),
            (new Vector3(-164.0f, 0.06f,   0.0f), new Vector3(-188.0f, 0.4f,   0.0f), "Gölet manzarası insana huzur veriyor.", "The lake view is so peaceful.")
        };

        /// <summary>
        /// 5-whisker dual-height local obstacle avoidance + polite right-hand sidewalk passing.
        /// Curves NPCs proactively around parked cars, trees, benches, stalls, planters, walls,
        /// other NPCs, and the player before any physical collision occurs.
        /// </summary>
        public static Vector3 ComputeSmartSteering(
            Transform self,
            Vector3 desiredDir,
            float probeDistance = 2.15f,
            float bodyRadius = 0.30f,
            bool avoidPlayer = true,
            bool avoidNeighbors = true)
        {
            desiredDir.y = 0f;
            if (desiredDir.sqrMagnitude < 0.0001f) return self != null ? self.forward : Vector3.forward;
            desiredDir.Normalize();
            if (self == null) return desiredDir;

            Vector3 pos = self.position;
            Vector3 bestDir = desiredDir;

            // 1. Evaluate 5 fan whiskers (0, +28, -28, +58, -58 deg) at dual height (knee + torso)
            float centerClearance = EvaluateWhiskerClearance(self, pos, desiredDir, probeDistance, bodyRadius);
            if (centerClearance < 0.96f)
            {
                float bestScore = centerClearance;
                for (int w = 1; w < WhiskerAngles.Length; w++)
                {
                    Vector3 candDir = Quaternion.Euler(0f, WhiskerAngles[w], 0f) * desiredDir;
                    float clear = EvaluateWhiskerClearance(self, pos, candDir, probeDistance, bodyRadius);
                    // Prefer gentle deflection and slight right-hand bias (Turkish sidewalk etiquette)
                    float anglePenalty = Mathf.Abs(WhiskerAngles[w]) * 0.0042f;
                    float rightBias = WhiskerAngles[w] > 0f ? 0.03f : 0f;
                    float score = clear - anglePenalty + rightBias;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDir = candDir;
                    }
                }
            }

            Vector3 steer = bestDir;

            // 2. Polite personal-space separation around the player (with right-hand pass bias!)
            if (avoidPlayer)
            {
                PlayerModeController mode = PlayerModeController.Instance;
                if (mode != null && !mode.IsDriving)
                {
                    Vector3 awayPlayer = pos - mode.ActiveWorldPosition;
                    awayPlayer.y = 0f;
                    float pDistSq = awayPlayer.sqrMagnitude;
                    if (pDistSq > 0.001f && pDistSq < 1.95f * 1.95f)
                    {
                        float pDist = Mathf.Sqrt(pDistSq);
                        float w = Mathf.InverseLerp(1.95f, 0.58f, pDist) * 1.55f;
                        Vector3 awayNorm = awayPlayer / pDist;
                        Vector3 passRight = Vector3.Cross(Vector3.up, bestDir);
                        steer += (awayNorm + passRight * 0.35f) * w;
                    }
                }
            }

            // 3. Polite separation from nearby TownPedestrians (with right-hand pass bias to avoid head-on deadlock)
            if (avoidNeighbors)
            {
                List<TownPedestrian> citizens = TownPedestrian.ActiveCitizens;
                for (int i = 0; i < citizens.Count; i++)
                {
                    TownPedestrian other = citizens[i];
                    if (other == null || other.transform == self) continue;
                    Vector3 away = pos - other.transform.position;
                    if (Mathf.Abs(away.y) > 1.8f) continue;
                    away.y = 0f;
                    float dSq = away.sqrMagnitude;
                    if (dSq > 0.001f && dSq < 1.38f * 1.38f)
                    {
                        float d = Mathf.Sqrt(dSq);
                        float w = Mathf.InverseLerp(1.38f, 0.48f, d) * 1.30f;
                        Vector3 rightStep = Vector3.Cross(Vector3.up, bestDir) * 0.32f;
                        steer += ((away / d) + rightStep) * w;
                    }
                }
            }

            steer.y = 0f;
            return steer.sqrMagnitude > 0.001f ? steer.normalized : bestDir;
        }

        private static float EvaluateWhiskerClearance(
            Transform self,
            Vector3 basePos,
            Vector3 dir,
            float probeDistance,
            float bodyRadius)
        {
            float worst = 1f;
            float r = Mathf.Clamp(bodyRadius * 0.85f, 0.14f, 0.34f);

            // Check both lower obstacle height (planters, chairs, car bumpers, crates) and torso height (walls, trunks, trucks)
            for (int h = 0; h < 2; h++)
            {
                float castY = h == 0 ? 0.44f : 1.08f;
                Vector3 origin = basePos + Vector3.up * castY;
                int count = Physics.SphereCastNonAlloc(origin, r, dir, SweepHits, probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = SweepHits[i];
                    if (hit.collider == null) continue;
                    if (hit.collider.transform.IsChildOf(self)) continue;
                    if (hit.normal.y > 0.62f) continue; // Ignore walkable ground, ramps & curbs
                    if (hit.collider.GetComponentInParent<PlayerFootController>() != null) continue;
                    if (hit.collider.GetComponentInParent<TownPedestrian>() != null) continue;
                    if (hit.collider.GetComponentInParent<LivingNpcColliderBridge>() != null) continue;

                    float frac = Mathf.Clamp01(hit.distance / Mathf.Max(0.1f, probeDistance));
                    if (frac < worst) worst = frac;
                }
            }
            return worst;
        }

        /// <summary>
        /// Dual-height collision-safe movement with wall sliding, corner escape, lightweight dynamic
        /// prop nudging, and kinematic Rigidbody synchronization so actors NEVER clip through walls or props.
        /// </summary>
        public static bool MoveActorSafely(
            Transform self,
            Rigidbody rb,
            Vector3 displacement,
            ref Vector3 persistentDir,
            bool steerPersistentDir,
            float bodyRadius = 0.30f,
            float lowHeight = 0.44f,
            float highHeight = 1.05f,
            bool clampWorldBounds = true)
        {
            displacement.y = 0f;
            float dist = displacement.magnitude;
            if (dist < 0.0001f || self == null) return false;

            Vector3 dir = displacement / dist;
            float checkDist = dist + bodyRadius + 0.14f;

            bool blocked = FindClosestBlockingHit(self, dir, checkDist, bodyRadius, lowHeight, highHeight, out RaycastHit closest);

            Vector3 finalMove = displacement;
            bool slid = false;

            if (blocked)
            {
                // If the obstacle is a lightweight DynamicWorldProp, nudge it slightly and slide cleanly around
                DynamicWorldProp dynProp = closest.collider != null
                    ? closest.collider.GetComponentInParent<DynamicWorldProp>()
                    : null;
                if (dynProp != null && dynProp.MassKg <= 22f)
                {
                    dynProp.ApplyNudge(dir * 1.4f, closest.point);
                }

                Vector3 flatNormal = closest.normal;
                flatNormal.y = 0f;
                if (flatNormal.sqrMagnitude > 0.001f)
                {
                    flatNormal.Normalize();
                    Vector3 slideDir = Vector3.ProjectOnPlane(dir, flatNormal);
                    if (slideDir.sqrMagnitude < 0.04f)
                    {
                        Vector3 rightTangent = Vector3.Cross(Vector3.up, flatNormal).normalized;
                        slideDir = Vector3.Dot(rightTangent, self.right) >= 0f ? rightTangent : -rightTangent;
                    }
                    else
                    {
                        slideDir.Normalize();
                    }

                    bool slideBlocked = FindClosestBlockingHit(self, slideDir, dist + bodyRadius + 0.08f, bodyRadius * 0.9f, lowHeight, highHeight, out _);
                    if (!slideBlocked)
                    {
                        finalMove = slideDir * (dist * 0.92f);
                        slid = true;
                        if (steerPersistentDir) persistentDir = slideDir;
                    }
                    else
                    {
                        // Try opposite tangent before backing out of a tight corner
                        Vector3 altSlide = -slideDir;
                        bool altBlocked = FindClosestBlockingHit(self, altSlide, dist + bodyRadius + 0.08f, bodyRadius * 0.9f, lowHeight, highHeight, out _);
                        if (!altBlocked)
                        {
                            finalMove = altSlide * (dist * 0.85f);
                            slid = true;
                            if (steerPersistentDir) persistentDir = altSlide;
                        }
                        else
                        {
                            // Cornered: gently push away from wall normal so actor never gets pinned
                            finalMove = flatNormal * (dist * 0.55f);
                            slid = true;
                            if (steerPersistentDir) persistentDir = flatNormal;
                        }
                    }
                }
                else
                {
                    finalMove = Vector3.zero;
                }
            }

            Vector3 nextPos = self.position + finalMove;
            if (clampWorldBounds)
            {
                nextPos.x = Mathf.Clamp(nextPos.x, -245f, 245f);
                nextPos.z = Mathf.Clamp(nextPos.z, -245f, 245f);
            }

            // Kinematic actors driven in Update() update transform.position immediately so multi-frame Update() never reads stale positions
            self.position = nextPos;
            return !blocked || slid;
        }

        private static bool FindClosestBlockingHit(
            Transform self,
            Vector3 dir,
            float checkDist,
            float radius,
            float lowY,
            float highY,
            out RaycastHit closest)
        {
            closest = default;
            float bestDist = float.MaxValue;
            bool blocked = false;

            int passes = highY > lowY + 0.15f ? 2 : 1;
            for (int p = 0; p < passes; p++)
            {
                float cy = p == 0 ? lowY : highY;
                Vector3 origin = self.position + Vector3.up * cy;
                int hitCount = Physics.SphereCastNonAlloc(origin, radius, dir, SweepHits, checkDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit h = SweepHits[i];
                    if (h.collider == null) continue;
                    if (h.collider.transform.IsChildOf(self)) continue;
                    if (h.normal.y > 0.62f) continue; // Ignore walkable floor / curb ramps
                    if (h.collider.GetComponentInParent<PlayerFootController>() != null) continue;
                    if (h.collider.GetComponentInParent<TownPedestrian>() != null) continue;
                    if (h.collider.GetComponentInParent<LivingNpcColliderBridge>() != null) continue;
                    if (h.collider.GetComponentInParent<MallShopperColliderBridge>() != null) continue;

                    if (h.distance < bestDist)
                    {
                        bestDist = h.distance;
                        closest = h;
                        blocked = true;
                    }
                }
            }
            return blocked;
        }

        /// <summary>
        /// Checks ALL approaching vehicles threatening an actor:
        /// 1. Player's driven SedanVehicleController
        /// 2. Player's ridden BicycleController / Motorcycle
        /// 3. Active AmbientTrafficManager vehicles on Hamidiye roads!
        /// </summary>
        public static ApproachingVehicleInfo DetectApproachingVehicle(Vector3 actorPos, Vector3 actorForward)
        {
            ApproachingVehicleInfo best = default;
            float bestThreatScore = 0f;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode != null && mode.IsDriving && mode.CurrentVehicle != null)
            {
                SedanVehicleController car = mode.CurrentVehicle;
                Vector3 moveDir = car.ForwardSpeedMs < -0.5f ? -car.transform.forward : car.transform.forward;
                EvaluateSingleVehicleThreat(actorPos, actorForward, car.transform.position, moveDir, car.SpeedKmh, 2.65f, ref best, ref bestThreatScore);
            }
            else if (mode != null && mode.CurrentMode == ControlMode.Cycling && mode.CurrentBicycle != null)
            {
                BicycleController bike = mode.CurrentBicycle;
                EvaluateSingleVehicleThreat(actorPos, actorForward, bike.transform.position, bike.transform.forward, bike.SpeedKmh, 1.85f, ref best, ref bestThreatScore);
            }

            // Also check nearby AI ambient traffic cars so pedestrians yield & step off roads cleanly!
            AmbientTrafficManager traffic = AmbientTrafficManager.Instance;
            if (traffic != null)
            {
                int count = traffic.ActiveVehicleCount;
                for (int i = 0; i < count; i++)
                {
                    if (!traffic.TryGetActiveVehicleState(i, out Vector3 vPos, out Vector3 vFwd, out float vSpeedMs)) continue;
                    EvaluateSingleVehicleThreat(actorPos, actorForward, vPos, vFwd, vSpeedMs * 3.6f, 2.45f, ref best, ref bestThreatScore);
                }
            }

            return best;
        }

        private static void EvaluateSingleVehicleThreat(
            Vector3 actorPos,
            Vector3 actorForward,
            Vector3 vehPos,
            Vector3 vehFwd,
            float speedKmh,
            float laneHalfWidth,
            ref ApproachingVehicleInfo best,
            ref float bestThreatScore)
        {
            Vector3 toPed = actorPos - vehPos;
            if (Mathf.Abs(toPed.y) > 2.6f) return;
            toPed.y = 0f;
            float dist = toPed.magnitude;
            if (dist > 14.5f || dist < 0.08f) return;

            vehFwd.y = 0f;
            if (vehFwd.sqrMagnitude < 0.001f) vehFwd = Vector3.forward;
            else vehFwd.Normalize();

            Vector3 vehRight = Vector3.Cross(Vector3.up, vehFwd).normalized;
            float fwdDist = Vector3.Dot(toPed, vehFwd);
            float lateral = Vector3.Dot(toPed, vehRight);
            float absLat = Mathf.Abs(lateral);

            float detectRange = Mathf.Clamp(4.8f + speedKmh * 0.17f, 4.8f, 13.5f);
            bool approaching = speedKmh > 2.2f && fwdDist > 0.30f && fwdDist <= detectRange && absLat <= laneHalfWidth;
            bool blockingClose = dist <= 3.5f && Vector3.Dot(actorForward, -toPed / dist) > 0.35f;
            if (!approaching && !blockingClose) return;

            bool urgent = approaching && speedKmh >= 16f && dist <= 8.2f && absLat <= laneHalfWidth * 0.85f;
            float score = (urgent ? 100f : 20f) + (15f - dist);
            if (score > bestThreatScore)
            {
                bestThreatScore = score;
                best = new ApproachingVehicleInfo
                {
                    Detected = true,
                    Urgent = urgent,
                    VehiclePos = vehPos,
                    VehicleForward = vehFwd,
                    VehicleRight = vehRight,
                    Distance = dist,
                    LateralOffset = lateral,
                    SpeedKmh = speedKmh
                };
            }
        }

        /// <summary>
        /// Finds the closest rain-sheltered awning, gazebo, or portico within maxRange so
        /// citizens intelligently hurry under cover when a summer shower begins.
        /// </summary>
        public static bool TryFindNearestRainShelter(Vector3 fromPos, float maxRange, out Vector3 shelterPos)
        {
            shelterPos = fromPos;
            float bestSq = maxRange * maxRange;
            bool found = false;
            for (int i = 0; i < RainShelterSpots.Length; i++)
            {
                Vector3 s = RainShelterSpots[i];
                if (Mathf.Abs(s.y - fromPos.y) > 2.2f) continue;
                Vector3 d = s - fromPos;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    shelterPos = new Vector3(s.x, fromPos.y, s.z);
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Finds a nearby neighborhood point-of-interest (bakery, simit cart, fountain, café, plaza)
        /// for realistic daily micro-stops.
        /// </summary>
        public static bool TryFindNearbyInterestSpot(
            Vector3 fromPos,
            float maxRange,
            out Vector3 faceTarget,
            out string lineTr,
            out string lineEn)
        {
            faceTarget = fromPos;
            lineTr = string.Empty;
            lineEn = string.Empty;
            float bestSq = maxRange * maxRange;
            bool found = false;
            for (int i = 0; i < DailyInterestSpots.Length; i++)
            {
                var spot = DailyInterestSpots[i];
                if (Mathf.Abs(spot.pos.y - fromPos.y) > 2.2f) continue;
                Vector3 d = spot.pos - fromPos;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    faceTarget = spot.faceTarget;
                    lineTr = spot.trLine;
                    lineEn = spot.enLine;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Produces a lively, localized contextual remark based on what Deniz is physically doing right now
        /// (carrying a heavy prop, soaked in rain, injured/low health, wearing accessories, driving a nice car, etc.).
        /// </summary>
        public static bool TryGetPlayerContextRemark(bool isEn, out string remark, out NpcBubbleMood mood)
        {
            remark = string.Empty;
            mood = NpcBubbleMood.Friendly;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return false;

            // 1. Player injured / low health -> caring neighborhood concern!
            if (PlayerNeedsController.Instance != null && PlayerNeedsController.Instance.Health < 42f)
            {
                remark = isEn
                    ? "Whoa Deniz, you look hurt! Rest up or visit Hamidiye Clinic!"
                    : "İyi misin Deniz?! Yüzün solgun, hemen bir şeyler ye ya da kliniğe uğra!";
                mood = NpcBubbleMood.Alert;
                return true;
            }

            // 2. Player carrying a physical prop (Damacana / crate / traffic cone)
            if (CarryableProp.IsHoldingProp && CarryableProp.CurrentlyHeld != null)
            {
                string label = CarryableProp.CurrentlyHeld.PropLabel;
                remark = isEn
                    ? $"Carrying that {label} around? Easy on your back, neighbor!"
                    : $"Kolay gelsin komşu, o {label} bayağı ağır duruyor!";
                mood = NpcBubbleMood.Friendly;
                return true;
            }

            // 3. Player soaked in Istanbul summer shower
            if (mode.FootController != null && mode.FootController.IsRainSoaked)
            {
                remark = isEn
                    ? "You're drenched from the rain, Deniz! Step under an awning!"
                    : "Sırılsıklam olmuşsun Deniz! Çabuk bir saçağın altına geç, üşüteceksin!";
                mood = NpcBubbleMood.Weather;
                return true;
            }

            // 4. Player driving a dealership car or sleek ride nearby
            if (mode.IsDriving && mode.CurrentVehicle != null)
            {
                SaveData save = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
                if (save != null && !string.IsNullOrEmpty(save.activeOwnedVehicleId) && save.activeOwnedVehicleId != "car_classic_sedan")
                {
                    remark = isEn
                        ? "Whoa, sweet new ride from the Auto Gallery! Drive safe!"
                        : "Vay vay, Oto Galeri'den yeni araba çekmişsin Deniz! Kazasız belasız!";
                    mood = NpcBubbleMood.Friendly;
                    return true;
                }
            }

            // 5. Player wearing stylish accessories (sunglasses / street cap)
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (d != null && d.ownsSunglasses && d.equipSunglasses && UnityEngine.Random.value < 0.45f)
            {
                remark = isEn
                    ? "Looking sharp in those sunglasses, Deniz!"
                    : "Güneş gözlükleri pek yakışmış Deniz, tarz olmuşsun!";
                mood = NpcBubbleMood.Friendly;
                return true;
            }
            if (d != null && d.ownsStreetCap && d.equipStreetCap && UnityEngine.Random.value < 0.45f)
            {
                remark = isEn
                    ? "Cool cap! Fresh off Hamidiye Atlas Mall?"
                    : "Şapka çok yakışmış komşu, Atlas AVM'den mi aldın?";
                mood = NpcBubbleMood.Friendly;
                return true;
            }

            // 6. Player currently cycling / motorcycle
            if (mode.CurrentMode == ControlMode.Cycling && mode.CurrentBicycle != null)
            {
                remark = mode.CurrentBicycle.IsMotorcycle
                    ? (isEn ? "Nice motorcycle! Careful on the avenue corners!" : "Motorun sesi harika ama sokak aralarında dikkatli sür komşu!")
                    : (isEn ? "Pedaling around Hamidiye! Great exercise!" : "Bisikletle tur atmak gibisi yok, kolay gelsin Deniz!");
                mood = NpcBubbleMood.Friendly;
                return true;
            }

            // 7. High neighborhood security heat warning
            if (NeighborhoodConsequenceSystem.Instance != null && NeighborhoodConsequenceSystem.Instance.CurrentHeat >= 2.2f)
            {
                remark = isEn
                    ? "Careful Deniz, Municipal Security is keeping an eye on the street..."
                    : "Dikkat et Deniz, zabıta bugün mahallede tetikte bekliyor...";
                mood = NpcBubbleMood.Alert;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Lightweight physics bridge placed on every LivingNeighborhoodPopulator NPC's Collider root
    /// so vehicles, bicycles, sprinting player, footballs, and thrown props physically collide with
    /// and trigger realistic reactions on all 68 living neighbors.
    /// </summary>
    public sealed class LivingNpcColliderBridge : MonoBehaviour
    {
        public int NpcIndex { get; private set; } = -1;

        public void Configure(int npcIndex)
        {
            NpcIndex = npcIndex;
        }

        public void NotifyBumpedByPlayer(Vector3 playerPos, Vector3 playerVelocity)
        {
            LivingNeighborhoodPopulator.Instance?.OnLivingNpcBumpedByPlayer(NpcIndex, playerPos, playerVelocity);
        }

        public void NotifyHitByVehicle(Vector3 carPos, Vector3 carVelocity, float speedKmh)
        {
            LivingNeighborhoodPopulator.Instance?.OnLivingNpcHitByVehicle(NpcIndex, carPos, carVelocity, speedKmh);
        }

        public void NotifyHitByThrownProp(Vector3 impactPos, Vector3 impactVelocity, string propLabel)
        {
            LivingNeighborhoodPopulator.Instance?.OnLivingNpcHitByProp(NpcIndex, impactPos, impactVelocity, propLabel);
        }
    }

    /// <summary>
    /// Lightweight physics bridge placed on every strolling Hamidiye Atlas AVM shopper's Collider root
    /// so players and thrown props cannot walk/pass through mall visitors and trigger realistic bumps.
    /// </summary>
    public sealed class MallShopperColliderBridge : MonoBehaviour
    {
        public int ShopperIndex { get; private set; } = -1;

        public void Configure(int shopperIndex)
        {
            ShopperIndex = shopperIndex;
        }

        public void NotifyBumpedByPlayer(Vector3 playerPos, Vector3 playerVelocity)
        {
            HamidiyeMallInteractionHandler.ActiveInstance?.OnShopperBumpedByPlayer(ShopperIndex, playerPos, playerVelocity);
        }
    }
}
