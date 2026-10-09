using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>
    /// A big rock heaved in a high arc by a unit with a thrower part. It's a heavy rigidbody: the
    /// impact knocks enemies flat, and it keeps rolling afterwards, bowling over anyone in its path
    /// while it's still moving fast. Each enemy can only be hit once per boulder.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Boulder : MonoBehaviour
    {
        const float Diameter = 1f;
        const float MinHitSpeed = 2.5f;   // slower than this it just bumps
        const float MaxKnock = 8f;
        const float Lifetime = 6f;

        static PhysicsMaterial rockMaterial;

        BattleUnit source;
        float damage, age;
        bool landed;
        Rigidbody rb;
        readonly HashSet<BattleUnit> struck = new();

        public static void Throw(BattleUnit source, BattleUnit target, float damage, Mesh mesh, Material material)
        {
            var go = new GameObject("Boulder");
            go.transform.SetPositionAndRotation(source.ThrowOrigin, Random.rotation);
            go.transform.localScale = Vector3.one * Diameter * 0.5f; // the mesh has a radius of about 1
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            else
            {
                // Fallback so a missing reference doesn't break the fight.
                var s = BattleVisuals.Primitive(PrimitiveType.Sphere, "Rock", go.transform, BattleVisuals.Lit(BattleVisuals.Palette.Stone));
                s.transform.localScale = Vector3.one * 2f;
            }

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.95f;
            col.material = rockMaterial ??= new PhysicsMaterial("Rock")
            {
                bounciness = 0.15f, dynamicFriction = 0.5f, staticFriction = 0.6f,
            };
            Physics.IgnoreCollision(col, source.GetComponent<Collider>());

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.angularDamping = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // A high lob that lands where the target is heading.
            var aim = target.transform.position + Vector3.up * 0.3f + target.Body.linearVelocity * 0.5f;
            var from = go.transform.position;
            var flat = new Vector3(aim.x - from.x, 0f, aim.z - from.z);
            float time = Mathf.Clamp(flat.magnitude / 6f, 0.7f, 1.3f);
            rb.linearVelocity = (aim - from - 0.5f * Physics.gravity * time * time) / time;
            rb.angularVelocity = Random.insideUnitSphere * 6f;

            var b = go.AddComponent<Boulder>();
            b.source = source;
            b.damage = damage;
            b.rb = rb;
        }

        void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            var point = c.GetContact(0).point;
            var unit = c.collider.GetComponentInParent<BattleUnit>();

            if (unit == null)
            {
                if (!landed && speed > 4f)
                {
                    landed = true;
                    BattleEffects.Spark(point, 0.9f, new Color(0.6f, 0.55f, 0.45f)); // dust
                    FindAnyObjectByType<BattleManager>()?.Shake(0.15f);
                }
                return;
            }

            if (!unit.IsAlive || struck.Contains(unit) || speed < MinHitSpeed) return;
            if (source != null && unit.Team == source.Team) return;
            struck.Add(unit);

            // Full damage from the air, less once it's rolling along the ground.
            float power = Mathf.Clamp01(speed / 9f);
            float dmg = damage * Mathf.Lerp(0.4f, 1f, power) * (landed ? 0.6f : 1f);
            float knock = Mathf.Min(MaxKnock, (2f + speed * 0.6f) * rb.mass / unit.Mass * 0.5f);
            unit.Hit(dmg, rb.linearVelocity, knock, point, knockDown: power > 0.35f);
        }

        void FixedUpdate()
        {
            // Rolling into the brook bogs it down.
            var arena = Arena.Current;
            if (rb.isKinematic || arena == null) return;
            var p = transform.position;
            var water = arena.WaterAt(p.x, p.z);
            if (water != null && p.y < water.waterLevel + Diameter * 0.4f)
                rb.linearVelocity *= 1f - 2.5f * Time.fixedDeltaTime;
        }

        void Update()
        {
            age += Time.deltaTime;
            // Sink into the ground at the end instead of popping out of existence.
            if (age > Lifetime - 0.6f)
            {
                rb.isKinematic = true;
                transform.position += Vector3.down * Time.deltaTime * 1.2f;
            }
            if (age > Lifetime) Destroy(gameObject);
        }
    }
}
