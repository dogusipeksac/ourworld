using System;
using System.Collections.Generic;
using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
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
        [Header("Pooled Prefab")]
        [SerializeField] private GameObject trafficVehiclePrefab;
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
            public int WaypointIndex;
            public float TargetCruiseSpeed;
            public float CurrentSpeed;
        }

        private readonly RaycastHit[] _castHits = new RaycastHit[8];
        private SimpleObjectPool _pool;
        private readonly List<ActiveAgent> _activeAgents = new List<ActiveAgent>();
        private int _currentMaxActive;
        private float _spawnCheckTimer;

        public void Configure(GameObject prefab, List<Transform> waypoints, List<TrafficStopLine> signalStops = null)
        {
            trafficVehiclePrefab = prefab;
            loopWaypoints = waypoints;
            if (signalStops != null)
            {
                stopLines = signalStops;
            }
        }

        public void SetStopLines(List<TrafficStopLine> signalStops)
        {
            stopLines = signalStops != null ? signalStops : new List<TrafficStopLine>();
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
            }
        }

        private void OnDestroy()
        {
            if (GraphicsProfileManager.Instance != null)
            {
                GraphicsProfileManager.Instance.OnTierChanged -= HandleGraphicsTierChanged;
            }
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

            for (int i = _activeAgents.Count - 1; i >= 0; i--)
            {
                ActiveAgent agent = _activeAgents[i];
                if (agent.Go == null)
                {
                    _activeAgents.RemoveAt(i);
                    continue;
                }

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
                agent.Go.transform.rotation = Quaternion.Slerp(agent.Go.transform.rotation, desiredRot, dt * 5f);

                bool mustStop = ShouldStopForTrafficLight(agent.Go.transform.position, dir) ||
                                HasForwardObstacle(agent.Go.transform, dir);

                float desiredSpeed = mustStop ? 0f : agent.TargetCruiseSpeed;
                float accel = mustStop ? 18f : 7.5f;
                agent.CurrentSpeed = Mathf.MoveTowards(agent.CurrentSpeed, desiredSpeed, accel * dt);

                if (agent.CurrentSpeed > 0.01f)
                {
                    agent.Go.transform.position += dir * (agent.CurrentSpeed * dt);
                }
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

        private bool HasForwardObstacle(Transform selfTr, Vector3 dir)
        {
            Vector3 origin = selfTr.position + Vector3.up * 0.75f + dir * 2.15f;
            int count = Physics.SphereCastNonAlloc(
                origin,
                0.85f,
                dir,
                _castHits,
                obstacleStopDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Transform hitTr = _castHits[i].transform;
                if (hitTr == null || hitTr == selfTr || hitTr.IsChildOf(selfTr))
                {
                    continue;
                }

                // Ignore flat ground/road colliders
                if (_castHits[i].normal.y > 0.78f)
                {
                    continue;
                }

                return true;
            }

            return false;
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
                float cruise = cruiseSpeed * UnityEngine.Random.Range(0.85f, 1.12f);
                _activeAgents.Add(new ActiveAgent
                {
                    Go = go,
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
