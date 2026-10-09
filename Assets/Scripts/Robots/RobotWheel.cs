using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// One wheel as raycast suspension: casts down from its mount, pushes the chassis up with a
    /// spring and damper, and grips the ground sideways and along its rolling direction. The robot's
    /// RobotDrive tells it how hard to drive; the wheel itself never has a collider, so any layout
    /// works and wheels can't snag on terrain edges.
    /// </summary>
    public class RobotWheel : MonoBehaviour
    {
        [Tooltip("Wheel radius in metres.")]
        public float radius = 0.22f;
        [Tooltip("Suspension travel from the mount to the wheel centre at rest, in metres.")]
        public float travel = 0.18f;
        [Tooltip("Sideways grip, as a friction coefficient. Above 1 is sticky racing rubber; ice is ~0.2.")]
        public float grip = 1.2f;
        [Tooltip("Grip along the rolling direction (driving, braking), as a friction coefficient.")]
        public float traction = 1.1f;
        [Tooltip("The spinning mesh. Moved down to the ground and rolled each frame.")]
        public Transform visual;

        public bool Grounded { get; private set; }
        /// <summary>-1 on the robot's left, +1 on its right. Decides which skid-steer track this wheel is on.</summary>
        public float Side { get; set; }
        /// <summary>How hard this wheel pushes on the ground this step, in newtons.</summary>
        public float Load { get; private set; }

        float compression, lastCompression, spin, groundSpeed;

        /// <summary>
        /// Applies this wheel's suspension and tyre forces for one physics step.
        /// drive: -1..1 throttle on this wheel's track. springK/damperC: per-wheel suspension.
        /// massShare: the slice of the robot's mass this wheel carries.
        /// </summary>
        public void Step(Rigidbody body, float drive, float motorForce, float maxSpeed, float rollingDrag,
                         float springK, float damperC, float massShare, float dt)
        {
            var up = transform.up;
            var origin = transform.position;
            float reach = travel + radius;
            Grounded = Physics.Raycast(origin, -up, out var hit, reach, ~0, QueryTriggerInteraction.Ignore)
                       && hit.rigidbody != body;
            if (!Grounded)
            {
                lastCompression = compression = 0f;
                Load = 0f;
                return;
            }

            // Suspension: spring on how far it's squashed, damper on how fast.
            compression = reach - hit.distance;
            float squashSpeed = (compression - lastCompression) / dt;
            lastCompression = compression;
            Load = Mathf.Max(0f, compression * springK + squashSpeed * damperC);
            body.AddForceAtPosition(up * Load, origin);

            // Tyre: work in the ground plane at the contact.
            var forward = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
            var right = Vector3.Cross(hit.normal, forward);
            var v = body.GetPointVelocity(hit.point);
            float vFwd = Vector3.Dot(v, forward);
            // Sideways uses the body's own slide, not this point's: spinning on the spot moves each
            // wheel sideways too, and gripping against that would stop skid steering from turning.
            float vSide = Vector3.Dot(body.linearVelocity, right);
            groundSpeed = vFwd;

            // Sideways: cancel this wheel's share of the slide within one step.
            float sideForce = -vSide * massShare / dt;

            // Along: motor force that fades out near top speed, or rolling drag when coasting.
            float along;
            if (Mathf.Abs(drive) > 0.01f)
            {
                float speedInDrive = vFwd * Mathf.Sign(drive);
                float headroom = Mathf.Clamp01(1f - speedInDrive / Mathf.Max(0.1f, maxSpeed));
                // Driving against the current motion (reversing a slide) always gets full force.
                if (speedInDrive < 0f) headroom = 1f;
                along = drive * motorForce * headroom;
            }
            else along = -vFwd * rollingDrag * massShare;

            // Friction limits: a wheel can only push as hard as it's pressed into the ground. Shoved
            // harder than that, it slides, which is what lets heavy robots bully light ones.
            sideForce = Mathf.Clamp(sideForce, -grip * Load, grip * Load);
            along = Mathf.Clamp(along, -traction * Load, traction * Load);
            body.AddForceAtPosition(forward * along + right * sideForce, hit.point);
        }

        void Update()
        {
            if (visual == null) return;
            float drop = Grounded ? travel - compression : travel;
            visual.localPosition = new Vector3(0f, -drop, 0f);
            spin += groundSpeed / Mathf.Max(0.01f, radius) * Mathf.Rad2Deg * Time.deltaTime;
            visual.localRotation = Quaternion.Euler(spin, 0f, 0f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position - transform.up * (travel + radius));
        }
    }
}
