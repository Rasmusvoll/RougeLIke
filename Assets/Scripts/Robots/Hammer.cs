using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>An overhead hammer or axe, like Deadblow: swings down onto whatever is in front.</summary>
    public class Hammer : RobotWeapon
    {
        Transform arm;
        float swingTime = -10f;
        bool landed = true;
        const float Rest = -100f, Down = 15f, SwingDuration = 0.14f;
        static readonly Vector3 ZoneCenter = new(0f, 0f, 0.75f), ZoneHalf = new(0.35f, 0.5f, 0.32f);

        public void Setup(RobotPartDefinition d, Transform a)
        {
            def = d;
            arm = a;
            arm.localRotation = Quaternion.Euler(Rest, 0f, 0f);
        }

        protected override void Update()
        {
            base.Update();
            float t = Time.time - swingTime;
            float angle = t < SwingDuration ? Mathf.Lerp(Rest, Down, t / SwingDuration)
                        : t < 0.35f ? Down
                        : Mathf.Lerp(Down, Rest, Mathf.Clamp01((t - 0.35f) / 0.7f));
            arm.localRotation = Quaternion.Euler(angle, 0f, 0f);

            if (!landed && t >= SwingDuration)
            {
                landed = true;
                if (Live)
                    foreach (var target in EnemiesIn(ZoneCenter, ZoneHalf))
                        Strike(target, def.damage, (Vector3.down + transform.forward * 0.3f) * def.force, target.CenterPosition + Vector3.up * 0.15f);
                // The blow bounces the hammer's own robot a little.
                Recoil(Vector3.up * def.force * 0.15f, transform.position + transform.forward * 0.5f);
            }

            if (!Live || cooldownLeft > 0f) return;
            if (EnemiesIn(ZoneCenter, ZoneHalf).Count == 0) return;
            swingTime = Time.time;
            landed = false;
            cooldownLeft = def.cooldown;
        }
    }
}
