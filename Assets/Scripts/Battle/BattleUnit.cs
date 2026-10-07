using System.Collections.Generic;
using RougeLike.Units;
using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>
    /// One physics-driven unit. It's a single rigidbody that keeps itself upright with a balance
    /// torque (like TABS), walks by pushing itself toward the nearest enemy, and lunges to attack.
    /// Hits shove the target and knock its balance out, so big hits make units stagger or fall over.
    /// On death balance is switched off and the parts break away. Stats come from the UnitInstance
    /// that UnitAssembler builds as this object's child.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BattleUnit : MonoBehaviour
    {
        const float AttackInterval = 1f;
        const float RetargetInterval = 0.5f;

        // Movement and balance, as accelerations so heavy and light bodies behave alike.
        const float MoveAccel = 14f;
        const float UprightStrength = 50f, UprightDamping = 9f;
        const float TurnStrength = 30f, TurnDamping = 7f;
        const float BalanceRecovery = 1f;    // per second
        const float ToppleAngle = 55f;        // tilt past this and the unit falls over
        const float ToppleTime = 1.1f;        // seconds on the ground before getting up

        // Hits.
        const float MeleeKnock = 1.8f, MeleeKnockPerDamage = 0.09f, MaxKnock = 6.5f;
        const float LungeSpeed = 3.5f;
        const float HitDelay = 0.12f;         // lunge travels a moment before the blow lands

        static PhysicsMaterial slippery, grippy;

        public UnitInstance Unit { get; private set; }
        public Team Team => Unit.Team;
        public bool IsAlive => Unit != null && Unit.IsAlive;
        public bool IsRanged { get; private set; }
        public float Radius { get; private set; }
        public float Mass => body.mass;
        public Rigidbody Body => body;
        /// <summary>Index into the player's blueprints, or -1 for enemies.</summary>
        public int BlueprintIndex { get; set; } = -1;

        public Vector3 CenterPosition => body.worldCenterOfMass;
        public Vector3 MuzzlePosition => transform.position + transform.rotation * new Vector3(0f, height * 0.6f, Radius);

        BattleManager battle;
        Rigidbody body;
        BoxCollider box;
        Transform visual, overlay;
        Vector3 visualScale;
        HealthBar bar;
        float height;

        BattleUnit target;
        float attackTimer, retargetTimer;
        readonly Dictionary<AbilityDefinition, float> abilityTimers = new();

        float balance = 1f, toppledUntil = -1f, walkPhase, squash;
        bool gettingUp;
        BattleUnit pendingHit;
        float pendingHitTime;

        public static BattleUnit Create(UnitBlueprint bp, Team team, IEnumerable<BuffDefinition> buffs,
                                        BattleManager battle, Vector3 position, Color teamColor)
        {
            var go = new GameObject(bp.name);
            go.transform.SetParent(battle.UnitRoot, false);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(team == Team.Player ? Vector3.forward : Vector3.back));
            var unit = UnitAssembler.Build(bp, buffs, battle.Database, go.transform, team);
            if (unit == null)
            {
                Destroy(go);
                return null;
            }
            go.AddComponent<Rigidbody>();
            var bu = go.AddComponent<BattleUnit>();
            bu.Init(unit, battle, teamColor);
            return bu;
        }

        void Init(UnitInstance unit, BattleManager b, Color teamColor)
        {
            Unit = unit;
            battle = b;
            visual = unit.transform;
            visualScale = visual.localScale;
            IsRanged = unit.Tags.Contains("ranged");

            // Size from the model: a box around the body (limbs trimmed a little) is the hit shape.
            var renderers = visual.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position + Vector3.up * 0.4f, Vector3.one * 0.8f);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.3f, 1.5f);
            height = Mathf.Max(bounds.max.y - transform.position.y, 0.5f);

            box = gameObject.AddComponent<BoxCollider>();
            var localCenter = transform.InverseTransformPoint(bounds.center);
            var size = transform.InverseTransformVector(bounds.size);
            size = new Vector3(Mathf.Abs(size.x) * 0.75f, Mathf.Abs(size.y), Mathf.Abs(size.z) * 0.8f);
            box.center = new Vector3(localCenter.x, size.y * 0.5f, localCenter.z);
            box.size = size;
            box.material = Slippery;

            body = GetComponent<Rigidbody>();
            body.mass = 1f + Unit.Stats.Get(StatType.MaxHealth) / 100f;
            body.linearDamping = 0.2f;
            body.angularDamping = 1.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // A low centre of mass keeps units planted until something really hits them.
            body.centerOfMass = new Vector3(box.center.x, height * 0.4f, box.center.z);
            body.isKinematic = true; // frozen until the fight starts

            // Ring and health bar live outside the body so they stay flat and upright when it tips.
            overlay = new GameObject($"{name} Overlay").transform;
            overlay.SetParent(battle.UnitRoot, false);
            var ring = BattleVisuals.Primitive(PrimitiveType.Cylinder, "Team Ring", overlay, BattleVisuals.Unlit(teamColor));
            ring.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            ring.transform.localScale = new Vector3(Radius * 1.5f, 0.005f, Radius * 1.5f);
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bar = HealthBar.Create(overlay, height + 0.35f, teamColor);
            bar.Set(1f);

            attackTimer = Random.Range(0.1f, 0.5f); // so a line of units doesn't swing in lockstep
            foreach (var ab in unit.Abilities) abilityTimers[ab] = ab.cooldown;
        }

        static PhysicsMaterial Slippery => slippery ??= new PhysicsMaterial("Unit")
        {
            dynamicFriction = 0.1f, staticFriction = 0.1f, frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0.05f,
        };

        static PhysicsMaterial Grippy => grippy ??= new PhysicsMaterial("Fallen")
        {
            dynamicFriction = 0.8f, staticFriction = 0.9f, frictionCombine = PhysicsMaterialCombine.Maximum,
        };

        void OnDestroy()
        {
            if (overlay != null) Destroy(overlay.gameObject);
        }

        /// <summary>Wakes the body when the battle starts.</summary>
        public void SetSimulated(bool on) => body.isKinematic = !on;

        float Tilt => Vector3.Angle(transform.up, Vector3.up);
        bool Toppled => Time.time < toppledUntil || gettingUp;
        bool Grounded => transform.position.y < 0.35f;

        // Decisions (per frame)

        void Update()
        {
            if (!IsAlive || battle.Phase != BattlePhase.Fighting) return;
            float dt = Time.deltaTime;
            TickAbilities(dt);

            if (pendingHit != null && Time.time >= pendingHitTime) LandHit();

            retargetTimer -= dt;
            if (target == null || !target.IsAlive || retargetTimer <= 0f)
            {
                target = battle.NearestEnemy(this);
                retargetTimer = RetargetInterval;
            }
            if (target == null || Toppled) return;

            var to = Flat(target.transform.position - transform.position);
            float gap = to.magnitude - Radius - target.Radius;
            bool facing = Vector3.Dot(Flat(transform.forward).normalized, to.normalized) > 0.6f;
            if (gap <= Reach && facing)
            {
                attackTimer -= dt;
                if (attackTimer <= 0f && balance > 0.15f)
                {
                    Attack(target);
                    attackTimer = AttackInterval;
                }
            }
            else
            {
                attackTimer = Mathf.Max(attackTimer, 0.25f); // a short wind-up after arriving
            }
        }

        /// <summary>Distance between body edges at which this unit can attack.</summary>
        float Reach => Mathf.Max(0.35f, Unit.Stats.Get(StatType.Range) - 0.6f);

        void Attack(BattleUnit t)
        {
            if (IsRanged)
            {
                Projectile.Fire(this, t, Unit.Stats.Get(StatType.Attack), new Color(0.55f, 1f, 0.3f));
                body.AddForce(-transform.forward * 1.2f, ForceMode.VelocityChange); // recoil
                return;
            }
            // Throw the body forward; the blow lands a moment later if the target is still in reach.
            var dir = Flat(t.transform.position - transform.position).normalized;
            body.AddForce(dir * LungeSpeed + Vector3.up * 0.8f, ForceMode.VelocityChange);
            pendingHit = t;
            pendingHitTime = Time.time + HitDelay;
        }

        void LandHit()
        {
            var t = pendingHit;
            pendingHit = null;
            if (!IsAlive || t == null || !t.IsAlive) return;
            var to = Flat(t.transform.position - transform.position);
            if (to.magnitude - Radius - t.Radius > Reach + 0.5f) return; // whiffed
            float damage = Unit.Stats.Get(StatType.Attack);
            float knock = Mathf.Min(MaxKnock, (MeleeKnock + damage * MeleeKnockPerDamage) * Mass / t.Mass);
            var point = Vector3.Lerp(t.CenterPosition, MuzzlePosition, 0.5f);
            t.Hit(damage, to.normalized, knock, point);
        }

        /// <summary>
        /// Deals damage and shoves the unit. Knock is a speed in m/s; it's applied above the centre of
        /// mass so strong hits tip the target over as well as pushing it back.
        /// </summary>
        public void Hit(float damage, Vector3 dir, float knock, Vector3 point)
        {
            if (!IsAlive) return;
            Unit.TakeDamage(damage);
            bar.Set(Unit.CurrentHealth / Mathf.Max(1f, Unit.Stats.Get(StatType.MaxHealth)));
            squash = 1f;

            dir = Flat(dir).normalized;
            if (!body.isKinematic)
            {
                var impulse = (dir + Vector3.up * 0.3f) * knock * body.mass;
                var high = new Vector3(point.x, Mathf.Max(point.y, body.worldCenterOfMass.y + height * 0.25f), point.z);
                body.AddForceAtPosition(impulse, high, ForceMode.Impulse);
                balance = Mathf.Max(0f, balance - knock / 6f);
            }
            battle.OnHit(point, knock, Team);
            if (!IsAlive) Die(dir * knock);
        }

        void Die(Vector3 push)
        {
            pendingHit = null;
            overlay.gameObject.SetActive(false);
            box.material = Grippy;
            body.angularDamping = 0.5f;
            // Ragdoll-ish: the body goes limp and the parts break off and fly.
            body.AddTorque(Random.onUnitSphere * 4f, ForceMode.VelocityChange);
            BreakOffParts(push);
            battle.OnUnitDied(this);
            Destroy(gameObject, 8f);
        }

        /// <summary>Kills a unit outright, e.g. when it's knocked off the field.</summary>
        public void Kill()
        {
            if (!IsAlive) return;
            Unit.TakeDamage(float.MaxValue);
            Die(Vector3.zero);
        }

        void BreakOffParts(Vector3 push)
        {
            var parts = new List<Transform>();
            foreach (Transform child in visual)
                if (child.name.Contains(": ")) parts.Add(child); // UnitAssembler names parts "slot: Part"
            foreach (var p in parts)
            {
                if (Random.value < 0.4f) continue; // some stay attached
                // Mirrored parts have negative scale, which box colliders can't take, so wrap each
                // part in an unscaled holder and put the physics there.
                var holder = new GameObject($"Debris {p.name}");
                holder.transform.SetParent(battle.UnitRoot, false);
                holder.transform.SetPositionAndRotation(p.position, p.rotation);
                p.SetParent(holder.transform, true);
                FitBox(holder).material = Grippy;
                var rb = holder.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.linearVelocity = body.linearVelocity + push * 0.6f + (p.position - CenterPosition).normalized * 2.5f + Vector3.up * 2.5f;
                rb.angularVelocity = Random.insideUnitSphere * 10f;
                Destroy(holder, 8f);
            }
        }

        void TickAbilities(float dt)
        {
            if (abilityTimers.Count == 0) return;
            foreach (var ab in new List<AbilityDefinition>(abilityTimers.Keys))
            {
                float t = abilityTimers[ab] - dt;
                if (t <= 0f)
                {
                    ab.Execute(Unit, battle.Context);
                    t = Mathf.Max(ab.cooldown, 0.1f);
                }
                abilityTimers[ab] = t;
            }
        }

        // Physics (per fixed step)

        void FixedUpdate()
        {
            if (body.isKinematic || !IsAlive) return;
            if (transform.position.y < -3f) { Kill(); return; } // knocked off the edge
            float dt = Time.fixedDeltaTime;

            // Falling over: past the tilt limit the unit goes limp, then pushes itself back up.
            if (!Toppled && Tilt > ToppleAngle)
            {
                toppledUntil = Time.time + ToppleTime;
                balance = 0f;
                box.material = Grippy;
            }
            if (toppledUntil > 0f && Time.time >= toppledUntil && !gettingUp)
            {
                if (Tilt > ToppleAngle) gettingUp = true;
                else EndTopple(); // rolled back onto its feet by itself
            }
            if (gettingUp && Tilt < 20f) EndTopple();

            float strength;
            if (Time.time < toppledUntil) strength = 0f;
            else if (gettingUp) strength = 2.5f;
            else
            {
                balance = Mathf.MoveTowards(balance, 1f, BalanceRecovery * dt);
                strength = Mathf.Lerp(0.15f, 1f, balance);
            }

            // Stay upright: torque toward world up, damped on the tipping axes.
            var w = body.angularVelocity;
            var axis = Vector3.Cross(transform.up, Vector3.up);
            var tipW = w - Vector3.Project(w, Vector3.up);
            body.AddTorque((axis * UprightStrength - tipW * UprightDamping) * strength, ForceMode.Acceleration);
            if (gettingUp && Grounded) body.AddForce(Vector3.up * 4f, ForceMode.Acceleration); // a little hop helps

            if (Time.time < toppledUntil || gettingUp || target == null || !target.IsAlive || battle.Phase != BattlePhase.Fighting)
                return;

            // Turn toward the target.
            var to = Flat(target.transform.position - transform.position);
            float yawErr = Vector3.SignedAngle(Flat(transform.forward), to, Vector3.up) * Mathf.Deg2Rad;
            body.AddTorque(Vector3.up * (yawErr * TurnStrength - w.y * TurnDamping) * strength, ForceMode.Acceleration);

            // Walk: steer the horizontal velocity toward the target, only with feet on the ground.
            float gap = to.magnitude - Radius - target.Radius;
            var vel = Flat(body.linearVelocity);
            var desired = gap > Reach ? to.normalized * Unit.Stats.Get(StatType.Speed) : Vector3.zero;
            if (Grounded) body.AddForce(Vector3.ClampMagnitude((desired - vel) * 6f, MoveAccel) * strength, ForceMode.Acceleration);

            // A TABS-ish waddle: rock side to side while walking.
            if (desired != Vector3.zero)
            {
                walkPhase += dt * (5f + Unit.Stats.Get(StatType.Speed));
                body.AddTorque(transform.forward * Mathf.Sin(walkPhase) * 6f * strength, ForceMode.Acceleration);
            }
        }

        void EndTopple()
        {
            gettingUp = false;
            toppledUntil = -1f;
            balance = 0.5f;
            box.material = Slippery;
        }

        void LateUpdate()
        {
            if (overlay != null && overlay.gameObject.activeSelf)
            {
                var p = transform.position;
                overlay.SetPositionAndRotation(new Vector3(p.x, Mathf.Max(0f, p.y), p.z), Quaternion.identity);
            }
            // A quick squash when hit.
            squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 6f);
            float s = 1f - squash * 0.15f;
            visual.localScale = Vector3.Scale(visualScale, new Vector3(1f / s, s, 1f / s));
        }

        /// <summary>A box collider around all of an object's meshes (they may sit on child objects).</summary>
        static BoxCollider FitBox(GameObject go)
        {
            var c = go.AddComponent<BoxCollider>();
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { c.size = Vector3.one * 0.2f; return c; }
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var t = go.transform;
            c.center = t.InverseTransformPoint(b.center);
            var s = t.InverseTransformVector(b.size);
            c.size = new Vector3(Mathf.Max(0.05f, Mathf.Abs(s.x)), Mathf.Max(0.05f, Mathf.Abs(s.y)), Mathf.Max(0.05f, Mathf.Abs(s.z)));
            return c;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
