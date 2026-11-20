using System.Collections.Generic;
using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Spawns and recycles lightweight ambient traffic vehicles (UAA City Props – Vehicles slot)
    /// strictly within a radius around the player using SimpleObjectPool.
    /// Automatically reduces density on Android Low graphics profile.
    /// </summary>
    public class AmbientTrafficManager : MonoBehaviour
    {
        [Header("Pooled Prefab (Replace visual with UAA City Props - Vehicles)")]
        [SerializeField] private GameObject trafficVehiclePrefab;
        [SerializeField] private int poolPrewarmCount = 8;

        [Header("Density & Distance Culling")]
        [SerializeField] private int maxActiveLowTier = 3;
        [SerializeField] private int maxActiveMediumTier = 6;
        [SerializeField] private float activeRadius = 115f;
        [SerializeField] private float minSpawnDistanceFromPlayer = 26f;
        [SerializeField] private float cruiseSpeed = 10.5f;
        [SerializeField] private float obstacleStopDistance = 7.5f;

        [Header("Looping Coastal Route Waypoints")]
        [SerializeField] private List<Transform> loopWaypoints = new List<Transform>();

        private class ActiveAgent
        {
            public GameObject Go;
            public int WaypointIndex;
            public float Speed;
        }

        private SimpleObjectPool _pool;
        private readonly List<ActiveAgent> _activeAgents = new List<ActiveAgent>();
        private int _currentMaxActive;
        private float _spawnCheckTimer;

        public void Configure(GameObject prefab, List<Transform> waypoints)
        {
            trafficVehiclePrefab = prefab;
            loopWaypoints = waypoints;
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
                _pool?.Release(_activeAgents[last].Go);
                _activeAgents.RemoveAt(last);
            }
        }

        private void Update()
        {
            if (_pool == null || loopWaypoints == null || loopWaypoints.Count < 2)
            {
                return;
            }

            Vector3 playerPos = PlayerModeController.Instance != null
                ? PlayerModeController.Instance.ActiveWorldPosition
                : Vector3.zero;

            // 1. Update active pooled vehicles & recycle those outside activeRadius.
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
                agent.Go.transform.rotation = Quaternion.Slerp(agent.Go.transform.rotation, desiredRot, Time.deltaTime * 5f);

                // Simple forward obstacle check so ambient cars yield to player or each other.
                Vector3 rayOrigin = agent.Go.transform.position + Vector3.up * 0.7f + dir * 2.2f;
                bool blocked = Physics.Raycast(rayOrigin, dir, obstacleStopDistance, ~0, QueryTriggerInteraction.Ignore);
                float moveSpeed = blocked ? 0f : agent.Speed;
                agent.Go.transform.position += dir * (moveSpeed * Time.deltaTime);
            }

            // 2. Periodically populate pooled vehicles on waypoints near the player.
            _spawnCheckTimer -= Time.deltaTime;
            if (_spawnCheckTimer <= 0f && _activeAgents.Count < _currentMaxActive)
            {
                _spawnCheckTimer = 1.2f;
                TrySpawnNearPlayer(playerPos);
            }
        }

        private void TrySpawnNearPlayer(Vector3 playerPos)
        {
            int count = loopWaypoints.Count;
            int startIndex = Random.Range(0, count);

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
                Vector3 dir = (loopWaypoints[nextIdx].position - wp.position);
                dir.y = 0f;
                Quaternion rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;

                GameObject go = _pool.Get(wp.position, rot);
                _activeAgents.Add(new ActiveAgent
                {
                    Go = go,
                    WaypointIndex = nextIdx,
                    Speed = cruiseSpeed * Random.Range(0.85f, 1.15f)
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
