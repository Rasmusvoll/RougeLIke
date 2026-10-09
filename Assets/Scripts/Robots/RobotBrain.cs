using System.Collections.Generic;
using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// Drives a robot on its own: picks the nearest enemy, swings round to the arena-centre side of it
    /// so a ram shoves it toward the edge, then charges. Feels for the ground ahead and backs off before
    /// driving off the edge or into a pit itself. Only sets RobotDrive's Throttle and Steer.
    /// </summary>
    [RequireComponent(typeof(RobotDrive))]
    public class RobotBrain : MonoBehaviour
    {
        [Tooltip("Within this distance it stops lining up and just rams.")]
        public float chargeRange = 2.2f;
        [Tooltip("How far behind the target (away from the edge) it lines up before charging.")]
        public float lineUpDistance = 1.6f;
        [Tooltip("Seconds of look-ahead when feeling for the edge. Higher is more careful.")]
        public float caution = 0.35f;

        public int Team { get; set; }
        public Transform Target { get; private set; }

        RobotDrive drive;
        float retargetTimer, reverseTimer, stallTime, reverseSteer;
        bool externalTarget;

        /// <summary>Lets whoever runs the fight pick the target (the battle does); the brain then stops picking its own.</summary>
        public void SetTarget(Transform target)
        {
            externalTarget = true;
            Target = target;
        }

        /// <summary>Every robot in the fight, set by whoever runs it. Enemies are those on another team.</summary>
        public IReadOnlyList<RobotBrain> Others { get; set; }

        void Awake() => drive = GetComponent<RobotDrive>();

        void FixedUpdate()
        {
            if (drive.Stuck || drive.GroundedWheels == 0)
            {
                drive.Throttle = drive.Steer = 0f;
                return;
            }

            retargetTimer -= Time.fixedDeltaTime;
            if (!externalTarget && (retargetTimer <= 0f || Target == null || !Alive(Target.GetComponent<RobotDrive>())))
            {
                var nearest = Nearest();
                Target = nearest != null ? nearest.transform : null;
                retargetTimer = 1f;
            }

            var pos = Flat(transform.position);
            var fwd = Flat(transform.forward).normalized;

            // Backing away from an edge we nearly drove off: reverse and turn toward the middle.
            if (reverseTimer > 0f)
            {
                reverseTimer -= Time.fixedDeltaTime;
                drive.Throttle = -1f;
                // Swing the nose back toward the middle, or the chosen way when backing out of a stall.
                drive.Steer = reverseSteer != 0f ? reverseSteer : SteerToward(fwd, -pos) * 0.8f;
                return;
            }

            if (Target == null)
            {
                drive.Throttle = 0f;
                drive.Steer = 0f;
                return;
            }

            var targetPos = Flat(Target.position);
            var aim = targetPos;
            float dist = Vector3.Distance(pos, targetPos);
            if (dist > chargeRange)
            {
                // Line up on the side of the target nearer the middle, so the ram pushes it outward.
                var outward = targetPos.sqrMagnitude > 0.01f ? targetPos.normalized : -fwd;
                aim = targetPos - outward * lineUpDistance;
                if (Vector3.Distance(pos, aim) < 0.6f) aim = targetPos;
            }

            var to = aim - pos;
            float steer = SteerToward(fwd, to);
            float angle = Vector3.Angle(fwd, to);
            float throttle = angle < 60f ? 1f : angle < 120f ? 0.4f : 0.15f;

            // Feel for the ground ahead. No ground (edge or pit) means stop and back off, unless the
            // enemy is right in front of us: then shove, it falls first.
            float speed = Mathf.Max(0f, drive.Speed);
            float look = 0.75f + speed * caution;
            bool shoving = dist < 1.2f && angle < 30f;
            if (!GroundAt(transform.position + fwd * look) && !shoving)
            {
                reverseTimer = 0.6f;
                reverseSteer = 0f;
                drive.Throttle = -1f;
                drive.Steer = 0f;
                return;
            }

            // Pinned against a stump or rock (or a pushing match gone nowhere): back out at an angle.
            stallTime = throttle > 0.5f && Mathf.Abs(drive.Speed) < 0.3f ? stallTime + Time.fixedDeltaTime : 0f;
            if (stallTime > (shoving ? 3f : 1f))
            {
                stallTime = 0f;
                reverseTimer = 0.8f;
                reverseSteer = Random.value < 0.5f ? -1f : 1f;
                drive.Throttle = -1f;
                drive.Steer = reverseSteer;
                return;
            }

            drive.Throttle = throttle;
            drive.Steer = steer;
        }

        RobotDrive Nearest()
        {
            if (Others == null) return null;
            RobotDrive best = null;
            float bestD = float.MaxValue;
            foreach (var o in Others)
            {
                if (o == null || o == this || o.Team == Team || !Alive(o.drive)) continue;
                float d = (o.transform.position - transform.position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = o.drive;
                }
            }
            return best;
        }

        // A robot whose brain was switched off has been knocked out: wreckage, not a target.
        static bool Alive(RobotDrive r) => r != null && r.isActiveAndEnabled && r.transform.position.y > -1.5f
                                           && (!r.TryGetComponent(out RobotBrain b) || b.enabled);

        static readonly RaycastHit[] Hits = new RaycastHit[8];

        static bool GroundAt(Vector3 p)
        {
            var arena = Arena.Current;
            if (arena != null && arena.InHole(p.x, p.z, 1.05f)) return false;
            // Robots don't count as ground: one standing at the edge mustn't hide the drop.
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 1.5f, Vector3.down, Hits, 3.5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (Hits[i].rigidbody == null || Hits[i].rigidbody.GetComponent<RobotDrive>() == null) return true;
            return false;
        }

        static float SteerToward(Vector3 fwd, Vector3 to)
        {
            if (to.sqrMagnitude < 1e-4f) return 0f;
            return Mathf.Clamp(Vector3.SignedAngle(fwd, to, Vector3.up) / 35f, -1f, 1f);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
