using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// A self-righting arm (a "srimech"): when the robot lies on its back or side and isn't moving,
    /// the arm kicks it back over onto its wheels.
    /// </summary>
    public class SelfRighter : RobotWeapon
    {
        Transform arm;
        float downTime, fireTime = -10f;

        public void Setup(RobotPartDefinition d, Transform a)
        {
            def = d;
            arm = a;
        }

        protected override void Update()
        {
            base.Update();
            float t = Time.time - fireTime;
            float angle = t < 0.12f ? Mathf.Lerp(0f, -150f, t / 0.12f) : Mathf.Lerp(-150f, 0f, Mathf.Clamp01((t - 0.12f) / 0.8f));
            arm.localRotation = Quaternion.Euler(angle, 0f, 0f);

            if (!Live || Body == null) return;
            bool down = Vector3.Dot(Body.transform.up, Vector3.up) < 0.3f && Body.linearVelocity.sqrMagnitude < 1f;
            downTime = down ? downTime + Time.deltaTime : 0f;
            if (downTime < 0.6f || cooldownLeft > 0f) return;
            downTime = 0f;
            fireTime = Time.time;
            cooldownLeft = def.cooldown;
            // Hop, and roll about the long axis toward upright: just fast enough to turn the way back
            // over while in the air (allowing for angular damping), so it lands on its wheels rather
            // than spinning on round onto its back again.
            var axis = Vector3.Cross(Body.transform.up, Vector3.up);
            if (axis.sqrMagnitude < 0.01f) axis = Body.transform.forward;
            float tilt = Vector3.Angle(Body.transform.up, Vector3.up) * Mathf.Deg2Rad;
            float flight = 2f * def.force / -Physics.gravity.y;
            float damp = Body.angularDamping;
            float spin = damp > 0.01f ? tilt * damp / (1f - Mathf.Exp(-damp * flight)) : tilt / flight;
            Body.AddForce(Vector3.up * def.force, ForceMode.VelocityChange);
            Body.angularVelocity = axis.normalized * spin;
        }
    }
}
