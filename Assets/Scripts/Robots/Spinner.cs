using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// A spinning weapon: a horizontal bar like Tombstone, or a vertical drum like Minotaur (spin axis
    /// along X). It spins up over `cooldown` seconds and hits with whatever speed it has, so a big hit
    /// slows it right down. Bars fling enemies sideways, drums launch them up; both kick back.
    /// </summary>
    public class Spinner : RobotWeapon
    {
        Transform rotor;
        Vector3 axis;
        float energy = 1f, angle;
        bool IsDrum => axis == Vector3.right;
        const float TopSpin = 1800f; // degrees per second at full speed

        public void Setup(RobotPartDefinition d, Transform r, Vector3 spinAxis)
        {
            def = d;
            rotor = r;
            axis = spinAxis;
        }

        public override void Arm(BattleUnit unit)
        {
            base.Arm(unit);
            energy = 0.5f;
            // The drum is a solid roller; a bar spins clear of the ground, so only its hub is solid.
            if (IsDrum)
            {
                var c = gameObject.AddComponent<CapsuleCollider>();
                c.center = rotor.localPosition;
                c.direction = 0;
                c.radius = 0.14f;
                c.height = 0.62f;
            }
            else AddBox(transform, rotor.localPosition, new Vector3(0.16f, 0.06f, 0.16f));
        }

        protected override void Update()
        {
            base.Update();
            // Builder: a lazy idle spin. Battle: winds up toward full speed.
            float speed = owner == null ? 0.15f : energy;
            if (owner != null && !Live) speed = energy = Mathf.MoveTowards(energy, 0f, Time.deltaTime * 0.5f);
            else if (owner != null) energy = Mathf.MoveTowards(energy, 1f, Time.deltaTime / Mathf.Max(0.2f, def.cooldown));
            angle += TopSpin * speed * Time.deltaTime;
            rotor.localRotation = Quaternion.AngleAxis(IsDrum ? angle : -angle, axis);

            if (!Live || energy < 0.25f) return;
            var center = rotor.localPosition + new Vector3(0f, IsDrum ? 0.05f : 0f, IsDrum ? 0.12f : 0.2f);
            var half = IsDrum ? new Vector3(0.35f, 0.2f, 0.2f) : new Vector3(0.52f, 0.18f, 0.32f);
            var targets = EnemiesIn(center, half);
            if (targets.Count == 0) return;

            var hub = rotor.position;
            foreach (var t in targets)
            {
                var toward = Vector3.ProjectOnPlane(t.CenterPosition - hub, Vector3.up).normalized;
                Vector3 push;
                if (IsDrum) push = (Vector3.up + transform.forward * 0.5f) * def.force * energy;
                else
                {
                    // The bar's edge moves sideways past the hub (spins clockwise seen from above).
                    var tangent = Vector3.Cross(toward, transform.up);
                    push = (tangent * 0.8f + toward * 0.5f + Vector3.up * 0.25f) * def.force * energy;
                }
                var point = Vector3.Lerp(hub, t.CenterPosition, 0.6f);
                Strike(t, def.damage * energy, push, point);
                Recoil(-push * 0.45f + (IsDrum ? Vector3.up * def.force * energy * 0.3f : Vector3.zero), hub);
            }
            energy *= 0.25f; // all that spin went into the hit
        }
    }
}
