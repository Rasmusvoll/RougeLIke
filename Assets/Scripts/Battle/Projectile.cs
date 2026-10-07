using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>
    /// A glob of acid lobbed in an arc by a ranged unit. It's a real rigidbody: it hits whatever it
    /// lands on, damages and shoves enemy units, and splats on the ground.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        const float Knock = 1.6f, KnockPerDamage = 0.08f;

        BattleUnit source;
        float damage;
        bool spent;
        Vector3 lastVelocity;

        void FixedUpdate() => lastVelocity = GetComponent<Rigidbody>().linearVelocity;

        public static void Fire(BattleUnit source, BattleUnit target, float damage, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.GetComponent<Renderer>().sharedMaterial = BattleVisuals.Unlit(color);
            go.transform.position = source.MuzzlePosition;
            go.transform.localScale = Vector3.one * 0.22f;
            var col = go.GetComponent<Collider>();
            Physics.IgnoreCollision(col, source.GetComponent<Collider>());

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Lob it so it lands where the target will roughly be.
            var aim = target.CenterPosition + target.Body.linearVelocity * 0.3f;
            var from = go.transform.position;
            var flat = new Vector3(aim.x - from.x, 0f, aim.z - from.z);
            float time = Mathf.Clamp(flat.magnitude / 9f, 0.25f, 0.9f);
            var g = Physics.gravity;
            rb.linearVelocity = (aim - from - 0.5f * g * time * time) / time;

            var p = go.AddComponent<Projectile>();
            p.source = source;
            p.damage = damage;
            p.lastVelocity = rb.linearVelocity;
            Destroy(go, 4f);
        }

        void OnCollisionEnter(Collision c)
        {
            if (spent) return;
            spent = true;
            var unit = c.collider.GetComponentInParent<BattleUnit>();
            var point = c.GetContact(0).point;
            if (unit != null && unit.IsAlive && (source == null || unit.Team != source.Team))
            {
                unit.Hit(damage, lastVelocity, Knock + damage * KnockPerDamage, point);
            }
            else
            {
                BattleEffects.Spark(point, 0.4f, new Color(0.55f, 1f, 0.3f));
            }
            Destroy(gameObject);
        }
    }
}
