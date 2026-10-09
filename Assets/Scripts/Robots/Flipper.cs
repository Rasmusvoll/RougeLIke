using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// A pneumatic flipper plate, like Chaos 2 or Bronco: when an enemy is on the plate it fires,
    /// throwing the enemy up from its near edge so it tumbles over backwards.
    /// </summary>
    public class Flipper : RobotWeapon
    {
        Transform hinge;
        float fireTime = -10f;
        static readonly Vector3 ZoneCenter = new(0f, 0.12f, 0.3f), ZoneHalf = new(0.4f, 0.22f, 0.32f);

        public void Setup(RobotPartDefinition d, Transform h)
        {
            def = d;
            hinge = h;
        }

        public override void Arm(BattleUnit unit)
        {
            base.Arm(unit);
            AddBox(transform, new Vector3(0f, -0.08f, 0.26f), new Vector3(0.72f, 0.04f, 0.46f));
        }

        protected override void Update()
        {
            base.Update();
            // Fire snaps up in a tenth of a second, then the plate settles back.
            float t = Time.time - fireTime;
            float angle = t < 0.1f ? Mathf.Lerp(0f, -70f, t / 0.1f) : Mathf.Lerp(-70f, 0f, Mathf.Clamp01((t - 0.1f) / 0.6f));
            hinge.localRotation = Quaternion.Euler(angle, 0f, 0f);

            if (!Live || cooldownLeft > 0f) return;
            var targets = EnemiesIn(ZoneCenter, ZoneHalf);
            if (targets.Count == 0) return;
            fireTime = Time.time;
            cooldownLeft = def.cooldown;
            foreach (var t2 in targets)
            {
                var toward = Vector3.ProjectOnPlane(t2.CenterPosition - transform.position, Vector3.up).normalized;
                // Lift the near edge: the target rotates away and over.
                var point = t2.CenterPosition - toward * 0.3f;
                Strike(t2, def.damage, Vector3.up * def.force + toward * def.force * 0.25f, point);
            }
            Recoil(Vector3.down * def.force * 0.25f, transform.position);
        }
    }
}
