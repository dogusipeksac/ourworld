using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.Vehicle;
using UnityEngine;

namespace OurWorld.World
{
    [Serializable]
    public class TrafficStopLine
    {
        public Vector3 stopPosition;
        public Vector3 approachDirection = Vector3.forward;
        public TrafficLightCycler trafficLight;
    }

    /// <summary>
    /// Spawns and recycles lightweight ambient traffic vehicles strictly within a radius around the player
    /// using SimpleObjectPool. Stops at red traffic light stop lines and brakes smoothly for the player car,
    /// pedestrians, and other vehicles within an 8 m forward SphereCast.
    /// </summary>
    public class AmbientTrafficManager : MonoBehaviour
    {
        public static AmbientTrafficManager Instance { get; private set; }

        [Header("Pooled Prefab")]
        [SerializeField] private GameObject trafficVehiclePrefab;
        [SerializeField] private GameObject drivableReplacementPrefab;
        [SerializeField] private int poolPrewarmCount = 8;

        [Header("Density & Distance Culling")]
        [SerializeField] private int maxActiveLowTier = 3;
        [SerializeField] private int maxActiveMediumTier = 6;
        [SerializeField] private float activeRadius = 115f;
        [SerializeField] private float minSpawnDistanceFromPlayer = 26f;
        [SerializeField] private float cruiseSpeed = 10.5f;
        [SerializeField] private float obstacleStopDistance = 8.0f;

        [Header("Looping Coastal Route Waypoints & Signal Stop Lines")]
        [SerializeField] private List<Transform> loopWaypoints = new List<Transform>();
        [SerializeField] private List<TrafficStopLine> stopLines = new List<TrafficStopLine>();

        private class ActiveAgent
        {
            public GameObject Go;
            public Rigidbody Rb;
            public int WaypointIndex;
            public float TargetCruiseSpeed;
            public float CurrentSpeed;
            public float PrevSpeed;
            public float BlockedByPlayerSeconds;
            public float HornCooldown;
            public List<Renderer> HeadlightRenderers;
            public List<Renderer> TaillightRenderers;
            public List<Transform> WheelTransforms;
            public Transform BodyVisual;
            public float BodyPitch;
            public float RainSprayTimer;
            public bool LastNightLightState;
            public bool LastBrakingLightState;
            public bool LightsInit;
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _lightMpb;
        public IReadOnlyList<TrafficStopLine> StopLines => stopLines;
        public int ActiveVehicleCount => _activeAgents.Count;

        public bool TryGetActiveVehicleState(int index, out Vector3 worldPos, out Vector3 forward, out float speedMs)
        {
            worldPos = Vector3.zero;
            forward = Vector3.forward;
            speedMs = 0f;
            if (index < 0 || index >= _activeAgents.Count) return false;
            ActiveAgent a = _activeAgents[index];
            if (a == null || a.Go == null || !a.Go.activeInHierarchy) return false;
            worldPos = a.Go.transform.position;
            forward = a.Go.transform.forward;
            speedMs = a.CurrentSpeed;
            return true;
        }

        private readonly RaycastHit[] _castHits = new RaycastHit[8];
        private SimpleObjectPool _pool;
        private readonly List<ActiveAgent> _activeAgents = new List<ActiveAgent>();
        private int _currentMaxActive;
        private float _spawnCheckTimer;

        private int _hijackedCounter;

        public void Configure(
            GameObject prefab,
            List<Transform> waypoints,
            List<TrafficStopLine> signalStops = null,
            GameObject drivablePrefab = null)
        {
            trafficVehiclePrefab = prefab;
            loopWaypoints = waypoints;
            if (signalStops != null)
            {
                stopLines = signalStops;
            }
            if (drivablePrefab != null)
            {
                drivableReplacementPrefab = drivablePrefab;
            }
        }

        public void SetStopLines(List<TrafficStopLine> signalStops)
        {
            stopLines = signalStops != null ? signalStops : new List<TrafficStopLine>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _currentMaxActive = maxActiveMediumTier;
            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged += HandleGraphicsTierChanged;
                HandleGraphicsTierChanged(GraphicsProfileManager.Instance.CurrentTier);
            }

            if (trafficVehiclePrefab != null && loopWaypoints != null && loopWaypoints.Count >= 2)
            {
                _pool = new SimpleObjectPool(trafficVehiclePrefab, transform, poolPrewarmCount);
                if (PlayerModeController.Instance != null)
                {
                    PlayerModeController.Instance.RefreshInteractablesCache();
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleGraphicsTierChanged;
            }
        }

        /// <summary>
        /// Converts an active ambient traffic car into a real WheelCollider drivable vehicle
        /// so the player can enter and drive ANY moving car in the open world.
        /// </summary>
        public SedanVehicleController TryHijackTrafficCar(GameObject trafficCarObj)
        {
            if (trafficCarObj == null || drivableReplacementPrefab == null)
            {
                return null;
            }

            ActiveAgent found = null;
            int foundIdx = -1;
            for (int i = 0; i < _activeAgents.Count; i++)
            {
                ActiveAgent agent = _activeAgents[i];
                if (agent.Go == null) continue;
                if (agent.Go == trafficCarObj || trafficCarObj.transform.IsChildOf(agent.Go.transform))
                {
                    found = agent;
                    foundIdx = i;
                    break;
                }
            }

            if (found == null)
            {
                return null;
            }

            Vector3 pos = found.Go.transform.position;
            Quaternion rot = found.Go.transform.rotation;

            if (_pool != null)
            {
                _pool.Release(found.Go);
            }
            _activeAgents.RemoveAt(foundIdx);

            _hijackedCounter++;
            GameObject drivableGo = Instantiate(drivableReplacementPrefab, pos + Vector3.up * 0.14f, rot);
            drivableGo.name = $"HijackedTrafficCar_{_hijackedCounter:00}";
            drivableGo.SetActive(true);

            SedanVehicleController controller = drivableGo.GetComponent<SedanVehicleController>();
            InteractableTrigger trig = drivableGo.GetComponent<InteractableTrigger>();
            if (trig == null)
            {
                trig = drivableGo.AddComponent<InteractableTrigger>();
            }
            trig.Configure(InteractableKind.TownVehicle, $"traffic_car_{_hijackedCounter:00}", "ui.btn.drive", 4.1f);

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }

            // Real-world carjacking consequence: alarm chirp, witness panic & Zabıta theft report!
            bool en = LocalizationService.IsEnglish;
            RealWorldDetailsSystem.PlayCarAlarmChirp(pos, highTone: true);
            NpcWorldAwareness.NotifyViolence(pos, pos, null);
            NeighborhoodConsequenceSystem conseq = NeighborhoodConsequenceSystem.EnsureInstance();
            conseq.PlayZabitaWhistle(0.62f);
            conseq.ReportOffense(
                NeighborhoodOffenseKind.CarTheft,
                pos,
                en ? "Stolen Traffic Car" : "Trafikten Alınan Araç",
                repPenalty: 20,
                immediateFineTl: 70,
                heatDelta: 1.75f);
            PlayerModeController.Instance?.RaiseStatusMessage(en
                ? "🚨 You forcibly took a car from traffic! The driver reported you to Municipal Security (-70 ₺ · -20 Reputation)."
                : "🚨 Trafikteki aracı zorla aldın! Sürücü zabıtayı aradı (-70 ₺ · -20 Saygınlık).");

            return controller;
        }

        private void HandleGraphicsTierChanged(GraphicsTier tier)
        {
            _currentMaxActive = tier == GraphicsTier.Low ? maxActiveLowTier : maxActiveMediumTier;
            while (_activeAgents.Count > _currentMaxActive)
            {
                int last = _activeAgents.Count - 1;
                if (_pool != null)
                {
                    _pool.Release(_activeAgents[last].Go);
                }
                _activeAgents.RemoveAt(last);
            }
        }

        private void Update()
        {
            if (_pool == null || loopWaypoints == null || loopWaypoints.Count < 2)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            Vector3 playerPos = PlayerModeController.Instance != null
                ? PlayerModeController.Instance.ActiveWorldPosition
                : Vector3.zero;

            bool isNight = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            bool isEn = LocalizationService.IsEnglish;

            for (int i = _activeAgents.Count - 1; i >= 0; i--)
            {
                ActiveAgent agent = _activeAgents[i];
                if (agent.Go == null)
                {
                    _activeAgents.RemoveAt(i);
                    continue;
                }

                UpdateAgentNightHeadlights(agent, isNight);

                float distToPlayer = Vector3.Distance(agent.Go.transform.position, playerPos);
                if (distToPlayer > activeRadius)
                {
                    _pool.Release(agent.Go);
                    _activeAgents.RemoveAt(i);
                    continue;
                }

                Transform targetWp = loopWaypoints[agent.WaypointIndex];
                if (targetWp == null) continue;

                Vector3 toTarget = targetWp.position - agent.Go.transform.position;
                toTarget.y = 0f;
                float remaining = toTarget.magnitude;

                if (remaining < 2.5f)
                {
                    agent.WaypointIndex = (agent.WaypointIndex + 1) % loopWaypoints.Count;
                    continue;
                }

                Vector3 dir = toTarget / remaining;
                Quaternion desiredRot = Quaternion.LookRotation(dir, Vector3.up);
                Quaternion nextRot = Quaternion.Slerp(agent.Go.transform.rotation, desiredRot, dt * 5f);

                bool redLight = ShouldStopForTrafficLight(agent.Go.transform.position, dir);
                float dynStopDist = Mathf.Clamp(obstacleStopDistance + agent.CurrentSpeed * 0.42f, 7.5f, 13.5f);
                bool obstacle = HasForwardObstacle(agent.Go.transform, dir, dynStopDist, out bool blockedByPlayer, out bool blockedByPedOrAnimal);
                bool mustStop = redLight || obstacle;

                if (agent.HornCooldown > 0f) agent.HornCooldown -= dt;
                if (!redLight && blockedByPlayer && distToPlayer < 14f)
                {
                    agent.BlockedByPlayerSeconds += dt;
                    if (agent.BlockedByPlayerSeconds >= 1.45f && agent.HornCooldown <= 0f)
                    {
                        agent.HornCooldown = 5.5f;
                        agent.BlockedByPlayerSeconds = 0.4f;
                        GameFeelAudio.PlayCarHorn();
                        string[] trHorns =
                        {
                            "Yoldan çekilsene kardeşim!",
                            "Trafiği tıkama, yürü!",
                            "Korna basıyorum, kenara geç!"
                        };
                        string[] enHorns =
                        {
                            "Hey! Get out of the road!",
                            "Don't block traffic, move aside!",
                            "Beep beep! Clear the lane!"
                        };
                        string line = isEn
                            ? enHorns[UnityEngine.Random.Range(0, enHorns.Length)]
                            : trHorns[UnityEngine.Random.Range(0, trHorns.Length)];
                        NpcSpeechBubbleManager.Say(
                            agent.Go.transform,
                            isEn ? "Driver" : "Sürücü",
                            line,
                            2.6f,
                            NpcBubbleMood.Alert,
                            3.0f);
                    }
                }
                else
                {
                    agent.BlockedByPlayerSeconds = Mathf.Max(0f, agent.BlockedByPlayerSeconds - dt * 2f);
                }

                float desiredSpeed = mustStop ? 0f : agent.TargetCruiseSpeed;
                float accel = mustStop ? (blockedByPedOrAnimal ? 24f : 18f) : 7.5f;
                agent.PrevSpeed = agent.CurrentSpeed;
                agent.CurrentSpeed = Mathf.MoveTowards(agent.CurrentSpeed, desiredSpeed, accel * dt);

                bool brakingNow = mustStop || (agent.PrevSpeed - agent.CurrentSpeed) > 0.35f * dt;
                UpdateAgentBrakeLights(agent, brakingNow, isNight);

                // Spin all 4 wheel meshes proportional to real forward travel speed
                if (agent.WheelTransforms != null && agent.CurrentSpeed > 0.05f)
                {
                    float spinDeg = (agent.CurrentSpeed * dt / 0.34f) * Mathf.Rad2Deg;
                    for (int w = 0; w < agent.WheelTransforms.Count; w++)
                    {
                        if (agent.WheelTransforms[w] != null)
                        {
                            agent.WheelTransforms[w].Rotate(spinDeg, 0f, 0f, Space.Self);
                        }
                    }
                }

                // Subtle braking nose-dive & acceleration squat on AI car chassis
                if (agent.BodyVisual != null)
                {
                    float longAccel = (agent.CurrentSpeed - agent.PrevSpeed) / Mathf.Max(0.001f, dt);
                    float targetPitch = Mathf.Clamp(-longAccel * 0.22f, -2.4f, 2.2f);
                    agent.BodyPitch = Mathf.Lerp(agent.BodyPitch, targetPitch, 1f - Mathf.Exp(-9f * dt));
                    agent.BodyVisual.localRotation = Quaternion.Euler(agent.BodyPitch, 0f, 0f);
                }

                // Wet rain tire spray behind moving AI cars during summer showers
                if (agent.CurrentSpeed > 4.2f && distToPlayer < 48f &&
                    NeighborhoodWeatherSystem.Instance != null &&
                    NeighborhoodWeatherSystem.Instance.CurrentWeather == NeighborhoodWeatherState.SummerShower)
                {
                    agent.RainSprayTimer -= dt;
                    if (agent.RainSprayTimer <= 0f)
                    {
                        agent.RainSprayTimer = 0.22f;
                        Vector3 rearSpray = agent.Go.transform.TransformPoint(new Vector3(0f, 0.16f, -1.65f));
                        VisualEffectsManager.SpawnWaterSplash(rearSpray, bigDive: false);
                    }
                }

                Vector3 nextPos = agent.Go.transform.position;
                if (agent.CurrentSpeed > 0.01f)
                {
                    nextPos += dir * (agent.CurrentSpeed * dt);
                }

                // Kinematic Rigidbody attached (isKinematic=true, ContinuousSpeculative) moves smoothly in Update() via transform sync
                agent.Go.transform.SetPositionAndRotation(nextPos, nextRot);
            }

            _spawnCheckTimer -= dt;
            if (_spawnCheckTimer <= 0f && _activeAgents.Count < _currentMaxActive)
            {
                _spawnCheckTimer = 1.2f;
                TrySpawnNearPlayer(playerPos);
            }
        }

        private bool ShouldStopForTrafficLight(Vector3 carPos, Vector3 moveDir)
        {
            if (stopLines == null || stopLines.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < stopLines.Count; i++)
            {
                TrafficStopLine line = stopLines[i];
                if (line == null || line.trafficLight == null || !line.trafficLight.IsRedOrAmber)
                {
                    continue;
                }

                // Check same lane direction
                if (Vector3.Dot(moveDir, line.approachDirection) < 0.65f)
                {
                    continue;
                }

                Vector3 toLine = line.stopPosition - carPos;
                toLine.y = 0f;

                float lateral = Mathf.Abs(Vector3.Dot(toLine, new Vector3(-moveDir.z, 0f, moveDir.x)));
                if (lateral > 3.6f)
                {
                    continue;
                }

                float forwardDist = Vector3.Dot(toLine, moveDir);
                // Stop if bumper is 0.4 m .. 9.0 m before the stop line (do not stop if already inside intersection)
                if (forwardDist >= 0.4f && forwardDist <= 9.0f)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateAgentNightHeadlights(ActiveAgent agent, bool isNight)
        {
            if (agent == null || agent.Go == null) return;
            if (agent.LightsInit && agent.LastNightLightState == isNight) return;

            if (agent.HeadlightRenderers == null)
            {
                agent.HeadlightRenderers = new List<Renderer>(4);
                agent.TaillightRenderers = new List<Renderer>(4);
                agent.WheelTransforms = new List<Transform>(4);
                agent.BodyVisual = agent.Go.transform.Find("VisualRoot") ?? agent.Go.transform.Find("Body");

                Renderer[] rends = agent.Go.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                {
                    if (rends[r] == null) continue;
                    string n = rends[r].gameObject.name;
                    if (n.IndexOf("Headlight", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        agent.HeadlightRenderers.Add(rends[r]);
                    }
                    else if (n.IndexOf("Taillight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             n.IndexOf("BrakeLight", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        agent.TaillightRenderers.Add(rends[r]);
                    }
                }

                Transform[] allT = agent.Go.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < allT.Length; t++)
                {
                    if (allT[t] == null || allT[t] == agent.Go.transform) continue;
                    string n = allT[t].name;
                    if (n.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        n.IndexOf("Steering", StringComparison.OrdinalIgnoreCase) < 0 &&
                        allT[t].GetComponent<WheelCollider>() == null)
                    {
                        agent.WheelTransforms.Add(allT[t]);
                    }
                }
            }

            agent.LightsInit = true;
            agent.LastNightLightState = isNight;
            if (_lightMpb == null) _lightMpb = new MaterialPropertyBlock();
            Color col = isNight ? new Color(1.0f, 0.94f, 0.76f) * 3.8f : Color.black;
            for (int r = 0; r < agent.HeadlightRenderers.Count; r++)
            {
                Renderer rend = agent.HeadlightRenderers[r];
                if (rend == null) continue;
                if (isNight)
                {
                    Material[] mats = rend.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] != null && !mats[m].IsKeywordEnabled("_EMISSION"))
                        {
                            mats[m].EnableKeyword("_EMISSION");
                        }
                    }
                }
                rend.GetPropertyBlock(_lightMpb);
                _lightMpb.SetColor(EmissionColorId, col);
                rend.SetPropertyBlock(_lightMpb);
            }
            UpdateAgentBrakeLights(agent, agent.LastBrakingLightState, isNight, force: true);
        }

        private void UpdateAgentBrakeLights(ActiveAgent agent, bool braking, bool isNight, bool force = false)
        {
            if (agent == null || agent.TaillightRenderers == null || agent.TaillightRenderers.Count == 0) return;
            if (!force && agent.LastBrakingLightState == braking) return;
            agent.LastBrakingLightState = braking;

            if (_lightMpb == null) _lightMpb = new MaterialPropertyBlock();
            Color tailCol = braking
                ? new Color(1.0f, 0.10f, 0.06f) * 4.6f
                : (isNight ? new Color(0.95f, 0.12f, 0.08f) * 1.35f : Color.black);
            for (int r = 0; r < agent.TaillightRenderers.Count; r++)
            {
                Renderer rend = agent.TaillightRenderers[r];
                if (rend == null) continue;
                if (braking || isNight)
                {
                    Material[] mats = rend.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] != null && !mats[m].IsKeywordEnabled("_EMISSION"))
                        {
                            mats[m].EnableKeyword("_EMISSION");
                        }
                    }
                }
                rend.GetPropertyBlock(_lightMpb);
                _lightMpb.SetColor(EmissionColorId, tailCol);
                rend.SetPropertyBlock(_lightMpb);
            }
        }

        private bool HasForwardObstacle(Transform selfTr, Vector3 dir, float castDistance, out bool blockedByPlayer, out bool blockedByPedOrAnimal)
        {
            blockedByPlayer = false;
            blockedByPedOrAnimal = false;
            Vector3 origin = selfTr.position + Vector3.up * 0.72f + dir * 2.15f;
            int count = Physics.SphereCastNonAlloc(
                origin,
                0.96f,
                dir,
                _castHits,
                castDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            bool anyBlocked = false;
            for (int i = 0; i < count; i++)
            {
                Transform hitTr = _castHits[i].transform;
                if (hitTr == null || hitTr == selfTr || hitTr.IsChildOf(selfTr))
                {
                    continue;
                }

                // Ignore flat ground/road colliders
                if (_castHits[i].normal.y > 0.76f)
                {
                    continue;
                }

                anyBlocked = true;
                if (hitTr.GetComponentInParent<PlayerFootController>() != null ||
                    hitTr.GetComponentInParent<BicycleController>() != null)
                {
                    blockedByPlayer = true;
                    blockedByPedOrAnimal = true;
                }
                else if (hitTr.GetComponentInParent<TownPedestrian>() != null ||
                         hitTr.GetComponentInParent<LivingNpcColliderBridge>() != null ||
                         hitTr.GetComponentInParent<StreetCatController>() != null ||
                         hitTr.GetComponentInParent<FarmAnimalController>() != null)
                {
                    blockedByPedOrAnimal = true;
                }
                else
                {
                    SedanVehicleController veh = hitTr.GetComponentInParent<SedanVehicleController>();
                    if (veh != null && veh.IsPlayerDriving)
                    {
                        blockedByPlayer = true;
                    }
                }
            }

            return anyBlocked;
        }

        private void TrySpawnNearPlayer(Vector3 playerPos)
        {
            int count = loopWaypoints.Count;
            int startIndex = UnityEngine.Random.Range(0, count);

            for (int offset = 0; offset < count; offset++)
            {
                int idx = (startIndex + offset) % count;
                Transform wp = loopWaypoints[idx];
                if (wp == null) continue;

                float dist = Vector3.Distance(wp.position, playerPos);
                if (dist < minSpawnDistanceFromPlayer || dist > activeRadius * 0.85f)
                {
                    continue;
                }

                if (IsSpaceOccupied(wp.position))
                {
                    continue;
                }

                int nextIdx = (idx + 1) % count;
                Vector3 dir = loopWaypoints[nextIdx].position - wp.position;
                dir.y = 0f;
                Quaternion rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;

                GameObject go = _pool.Get(wp.position, rot);
                Rigidbody trafficRb = WorldPhysicsSolidifier.EnsureKinematicRigidbody(go);
                InteractableTrigger trig = go.GetComponent<InteractableTrigger>();
                if (trig == null)
                {
                    trig = go.AddComponent<InteractableTrigger>();
                    trig.Configure(InteractableKind.TownVehicle, "traffic_car", "ui.btn.drive", 4.4f);
                    if (PlayerModeController.Instance != null)
                    {
                        PlayerModeController.Instance.RefreshInteractablesCache();
                    }
                }
                float cruise = cruiseSpeed * UnityEngine.Random.Range(0.85f, 1.12f);
                _activeAgents.Add(new ActiveAgent
                {
                    Go = go,
                    Rb = trafficRb,
                    WaypointIndex = nextIdx,
                    TargetCruiseSpeed = cruise,
                    CurrentSpeed = cruise * 0.5f
                });
                return;
            }
        }

        private bool IsSpaceOccupied(Vector3 pos)
        {
            foreach (ActiveAgent agent in _activeAgents)
            {
                if (agent.Go != null && (agent.Go.transform.position - pos).sqrMagnitude < 144f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
