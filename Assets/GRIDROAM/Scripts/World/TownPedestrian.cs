using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Sidewalk pedestrian that strolls back and forth between two world points at 1.3 m/s,
    /// turns smoothly at endpoints, and drives a ProceduralHumanoid with a kinematic CapsuleCollider.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class TownPedestrian : MonoBehaviour
    {
        [SerializeField] private Vector3 pointA;
        [SerializeField] private Vector3 pointB;
        [SerializeField] private float walkSpeed = 1.3f;
        [SerializeField] private float turnSpeed = 6f;
        [SerializeField] private float endpointPauseSeconds = 0.8f;
        [SerializeField] private ProceduralHumanoid humanoid;

        private bool _headingToB = true;
        private float _pauseTimer;
        private float _currentSpeed;

        public void Configure(Vector3 a, Vector3 b, ProceduralHumanoid hum, float speed = 1.3f, bool startHeadingToB = true)
        {
            pointA = a;
            pointB = b;
            humanoid = hum;
            walkSpeed = speed;
            _headingToB = startHeadingToB;
        }

        private void Awake()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (humanoid == null)
            {
                humanoid = GetComponentInChildren<ProceduralHumanoid>();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            Vector3 target = _headingToB ? pointB : pointA;
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            float desiredSpeed = 0f;

            if (_pauseTimer > 0f)
            {
                _pauseTimer -= dt;
                desiredSpeed = 0f;
            }
            else if (dist < 0.35f)
            {
                _headingToB = !_headingToB;
                _pauseTimer = endpointPauseSeconds;
                desiredSpeed = 0f;
            }
            else
            {
                Vector3 dir = toTarget / dist;
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * dt));

                // Gently slow down while turning around
                float facingDot = Mathf.Clamp01(Vector3.Dot(transform.forward, dir));
                desiredSpeed = walkSpeed * Mathf.Lerp(0.35f, 1f, facingDot);
                transform.position += transform.forward * (_currentSpeed * dt);
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, desiredSpeed, 4.5f * dt);

            if (humanoid != null)
            {
                humanoid.SetLocomotion(_currentSpeed, grounded: true, verticalVelocity: 0f);
            }
        }
    }
}
