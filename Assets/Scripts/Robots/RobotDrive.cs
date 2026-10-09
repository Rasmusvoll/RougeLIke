using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// Drives a wheeled robot like a tank: wheels left of centre run on the left track, wheels right of
    /// centre on the right, and turning comes from running the tracks at different speeds. Works with
    /// any wheel layout. Whoever controls the robot (keyboard now, the battle AI later) just sets
    /// Throttle and Steer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RobotDrive : MonoBehaviour
    {
        [Header("Motor")]
        [Tooltip("Total push of all motors, in newtons per kilogram of robot. Higher accelerates harder.")]
        public float power = 14f;
        [Tooltip("Top speed in m/s.")]
        public float maxSpeed = 6f;
        [Tooltip("How quickly it can spin on the spot, in degrees per second.")]
        public float turnRate = 200f;
        [Tooltip("Coasting slowdown with no throttle. High values act like brakes, so the robot is harder to shove.")]
        public float rollingDrag = 2.5f;

        [Header("Suspension")]
        [Tooltip("Bounce frequency in Hz. Low is floaty, high is stiff.")]
        public float springFrequency = 3f;
        [Tooltip("0 bounces forever, 1 settles without overshoot.")]
        public float damping = 0.45f;

        [Header("Control (set by whoever drives)")]
        [Range(-1f, 1f)] public float Throttle;
        [Range(-1f, 1f)] public float Steer;

        public Rigidbody Body { get; private set; }
        public IReadOnlyList<RobotWheel> Wheels => wheels;
        public int GroundedWheels { get; private set; }
        public float Speed => Vector3.Dot(Body.linearVelocity, transform.forward);
        /// <summary>On its back or side with no wheel touching for a while: can't drive any more.</summary>
        public bool Stuck => stuckTime > 1.5f;

        readonly List<RobotWheel> wheels = new();
        float stuckTime;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            RefreshWheels();
        }

        /// <summary>Finds the wheels under this robot and sorts them onto the left or right track.</summary>
        public void RefreshWheels()
        {
            wheels.Clear();
            GetComponentsInChildren(wheels);
            foreach (var w in wheels)
            {
                float x = transform.InverseTransformPoint(w.transform.position).x;
                // A wheel on the centre line drives with both tracks' average, i.e. only goes straight.
                w.Side = Mathf.Abs(x) < 0.05f ? 0f : Mathf.Sign(x);
            }
        }

        void FixedUpdate()
        {
            if (wheels.Count == 0) return;
            float dt = Time.fixedDeltaTime;
            float massShare = Body.mass / wheels.Count;
            // Suspension tuned from the robot's own weight, so heavy builds don't bottom out.
            float omega = 2f * Mathf.PI * springFrequency;
            float springK = massShare * omega * omega;
            float damperC = 2f * damping * massShare * omega;
            float motorPerWheel = power * Body.mass / wheels.Count;

            float throttle = Mathf.Clamp(Throttle, -1f, 1f);
            float steer = Mathf.Clamp(Steer, -1f, 1f);
            // Reversing steers the way a tank does: the nose still swings toward the stick.
            float left = Mathf.Clamp(throttle + steer, -1f, 1f);
            float right = Mathf.Clamp(throttle - steer, -1f, 1f);

            int grounded = 0;
            foreach (var w in wheels)
            {
                float drive = w.Side < 0f ? left : w.Side > 0f ? right : throttle;
                w.Step(Body, drive, motorPerWheel, maxSpeed, rollingDrag, springK, damperC, massShare, dt);
                if (w.Grounded) grounded++;
            }
            GroundedWheels = grounded;

            if (grounded > 0)
            {
                // Real skid steering scrubs the tyres sideways. The wheels leave spin alone (see
                // RobotWheel), so this turns the body toward the wanted spin instead, and holds it
                // straight with no steer. Weaker with wheels off the ground, so a tipped robot turns badly.
                float share = (float)grounded / wheels.Count;
                var up = transform.up;
                float yaw = Vector3.Dot(Body.angularVelocity, up);
                float wanted = steer * turnRate * Mathf.Deg2Rad;
                float yawAccel = Mathf.Clamp((wanted - yaw) / dt, -40f, 40f) * share;
                Body.AddTorque(up * yawAccel, ForceMode.Acceleration);
            }

            bool upright = Vector3.Dot(transform.up, Vector3.up) > 0.35f;
            stuckTime = grounded == 0 && !upright && Body.linearVelocity.sqrMagnitude < 1f ? stuckTime + dt : 0f;
        }
    }
}
