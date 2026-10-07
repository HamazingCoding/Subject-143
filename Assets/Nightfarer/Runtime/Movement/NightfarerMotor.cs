using System;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// CharacterController-based physics. Knows nothing about states, input or animation: states ask it to
    /// accelerate, rotate, displace (for dodges/lunges) or jump, and the character calls Simulate once per frame.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NightfarerMotor : MonoBehaviour
    {
        public LayerMask groundMask = ~0;
        public float groundProbeDistance = 0.2f;

        public CharacterController Controller { get; private set; }
        public NightfarerConfig Config { get; set; }
        public Vector3 PlanarVelocity { get; private set; }
        public float VerticalVelocity { get; private set; }
        public bool Grounded { get; private set; }
        public float TimeSinceGrounded { get; private set; }
        public float FallHeight { get; private set; }
        public bool GravityEnabled { get; set; } = true;
        /// <summary>Actual displacement per second achieved by the last Simulate (after collisions).</summary>
        public Vector3 ActualVelocity { get; private set; }

        /// <summary>(fall height in metres, impact speed in m/s)</summary>
        public event Action<float, float> Landed;

        Vector3 pendingDisplacement;
        float peakY;
        readonly RaycastHit[] probeHits = new RaycastHit[8];

        void Awake()
        {
            Controller = GetComponent<CharacterController>();
            Grounded = true;
            peakY = transform.position.y;
        }

        public float PlanarSpeed => PlanarVelocity.magnitude;

        public void Accelerate(Vector3 targetVelocity, float acceleration, float deceleration, float dt)
        {
            targetVelocity.y = 0f;
            Vector3 current = PlanarVelocity;
            float rate = targetVelocity.sqrMagnitude >= current.sqrMagnitude ? acceleration : deceleration;
            if (Config != null && current.sqrMagnitude > 1f && targetVelocity.sqrMagnitude > 0.01f &&
                Vector3.Angle(current, targetVelocity) > Config.pivotAngle)
                rate = deceleration * Config.pivotDecelerationMultiplier;
            PlanarVelocity = Vector3.MoveTowards(current, targetVelocity, rate * dt);
        }

        public void SetPlanarVelocity(Vector3 v)
        {
            v.y = 0f;
            PlanarVelocity = v;
        }

        public void SetVerticalVelocity(float v) => VerticalVelocity = v;

        public void AddDisplacement(Vector3 delta) => pendingDisplacement += delta;

        public void Jump(float height)
        {
            VerticalVelocity = Mathf.Sqrt(2f * height * -Config.gravity);
            Grounded = false;
            TimeSinceGrounded = 1f;
            peakY = transform.position.y;
        }

        /// <summary>Rotate around Y toward dir. degPerSec &lt;= 0 snaps instantly.</summary>
        public void FaceDirection(Vector3 dir, float degPerSec, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-5f) return;
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = degPerSec <= 0f ? target : Quaternion.RotateTowards(transform.rotation, target, degPerSec * dt);
        }

        public void Simulate(float dt)
        {
            if (dt <= 0f) return;
            if (GravityEnabled)
            {
                if (Grounded && VerticalVelocity < 0f) VerticalVelocity = -2f;
                else VerticalVelocity = Mathf.Max(VerticalVelocity + Config.gravity * dt, Config.maxFallSpeed);
            }

            Vector3 start = transform.position;
            Vector3 motion = (PlanarVelocity + Vector3.up * VerticalVelocity) * dt + pendingDisplacement;
            // Keep displacement-driven moves (rolls, lunges) glued to slopes and stairs.
            if (Grounded && GravityEnabled && VerticalVelocity <= 0f) motion.y -= Controller.stepOffset * 0.5f;
            pendingDisplacement = Vector3.zero;

            CollisionFlags flags = Controller.Move(motion);
            ActualVelocity = (transform.position - start) / dt;

            if ((flags & CollisionFlags.Above) != 0 && VerticalVelocity > 0f) VerticalVelocity = 0f;

            bool rising = VerticalVelocity > 0.5f;
            bool onGround = !rising && ((flags & CollisionFlags.Below) != 0 || ProbeGround());

            if (onGround)
            {
                if (!Grounded)
                {
                    float fall = Mathf.Max(0f, peakY - transform.position.y);
                    float impact = -VerticalVelocity;
                    Grounded = true;
                    TimeSinceGrounded = 0f;
                    VerticalVelocity = -2f;
                    Landed?.Invoke(fall, impact);
                }
                Grounded = true;
                TimeSinceGrounded = 0f;
                peakY = transform.position.y;
            }
            else
            {
                if (Grounded)
                {
                    Grounded = false;
                    peakY = transform.position.y;
                }
                TimeSinceGrounded += dt;
                peakY = Mathf.Max(peakY, transform.position.y);
            }
            FallHeight = Grounded ? 0f : Mathf.Max(0f, peakY - transform.position.y);
        }

        bool ProbeGround()
        {
            float radius = Controller.radius * 0.9f;
            Vector3 bottom = transform.position + Controller.center + Vector3.down * (Controller.height * 0.5f - Controller.radius);
            Vector3 origin = bottom + Vector3.up * 0.1f;
            int n = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, probeHits, groundProbeDistance + 0.1f, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = probeHits[i].collider;
                if (c == Controller || c.transform.IsChildOf(transform)) continue;
                if (probeHits[i].distance <= 0f) continue;
                return true;
            }
            return false;
        }

        /// <summary>Teleport (used by the test scene reset).</summary>
        public void Warp(Vector3 position, Quaternion rotation)
        {
            Controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            Controller.enabled = true;
            PlanarVelocity = Vector3.zero;
            VerticalVelocity = 0f;
            pendingDisplacement = Vector3.zero;
            peakY = position.y;
        }
    }
}
