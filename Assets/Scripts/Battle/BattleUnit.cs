using System.Collections.Generic;
using RougeLike.Units;
using RougeLike.Robots;
using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>
    /// One physics-driven unit. It's a single rigidbody that keeps itself upright with a balance
    /// torque (like TABS), walks by pushing itself toward the nearest enemy, and lunges to attack.
    /// Hits shove the target and knock its balance out, so big hits make units stagger or fall over.
    /// On death balance is switched off and the parts break away. Stats come from the UnitInstance
    /// that UnitAssembler builds as this object's child.
    /// How it moves comes from its legs (Gait) and where they are: each foot pushes from where it
    /// stands, so legs at the tail drag a sagging body along and lopsided legs pull it into a curve.
    /// With no legs it sits planted like a turret, only turning to face its target and attacking
    /// whatever comes in reach. Attacks come from the attack part itself: up close the unit hauls
    /// its body round until that part (a claw on the side, a tail behind) points at the target, then
    /// flings it. A unit with no attack part only kicks, and only if it has legs.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BattleUnit : MonoBehaviour
    {
        const float AttackInterval = 1f, ThrowInterval = 2.4f;
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
        const float KickDamage = 0.35f, KickKnock = 0.6f; // a unit with no attack part kicks, weakly
        const float MaxFootAccel = 30f;       // the most one foot can shove the body, m/s²
        const float FootTurnShare = 0.65f;    // share of turning done by feet pushing sideways where they stand
        const float BellyDrag = 4f;           // ground drag on an end no legs hold up, per second

        const float WaterDrag = 2.5f;         // extra damping while wading, per second

        // Counterweight: attacks throw the attacker's own body around, more so for heavy weapons on light bodies.
        const float WindupPull = 7f, WindupLift = 5f; // hauling the weapon back, m/s² at full wind-up
        const float ThrowYank = 2.4f;         // follow-through after a throw, m/s at the throwing arm
        const float SpitKick = 1.6f;          // recoil of a spit, m/s at the mouth

        static PhysicsMaterial slippery, grippy;

        public UnitInstance Unit { get; private set; }
        /// <summary>A wheeled robot (its body is a ChassisDefinition): RobotDrive and RobotBrain move it, not legs.</summary>
        public bool IsRobot { get; private set; }
        /// <summary>The fight is on and this unit's body is simulated.</summary>
        public bool Fighting => battle != null && battle.Phase == BattlePhase.Fighting && body != null && !body.isKinematic;
        public Team Team => Unit.Team;
        public bool IsAlive => Unit != null && Unit.IsAlive;
        public bool IsRanged { get; private set; }
        /// <summary>Has a part tagged "thrower": lobs boulders instead of spitting or biting.</summary>
        public bool IsThrower { get; private set; }
        public float Radius { get; private set; }
        public float Mass => body.mass;
        public Rigidbody Body => body;
        /// <summary>Index into the player's blueprints, or -1 for enemies.</summary>
        public int BlueprintIndex { get; set; } = -1;

        public Vector3 CenterPosition => body.worldCenterOfMass;
        /// <summary>Where the unit's attack comes from: its claw, horn, mouth or tail tip.</summary>
        public Vector3 AttackPoint => transform.TransformPoint(attackLocal);
        /// <summary>The flat way the attack part reaches. Up close the unit turns to aim this at its target.</summary>
        public Vector3 AttackDir => Flat(transform.TransformDirection(attackDirLocal)).normalized;
        public Vector3 MuzzlePosition => AttackPoint + Vector3.up * 0.1f;
        /// <summary>How well the legs hold the body up, 0 to 1; the rest drags on the ground.</summary>
        public float Support => support;
        public int FootCount => feet.Count;
        public Vector3 ThrowOrigin
        {
            get
            {
                var p = AttackPoint;
                return new Vector3(p.x, transform.position.y + height + 0.3f, p.z); // held up over the throwing arm
            }
        }

        BattleManager battle;
        Rigidbody body;
        BoxCollider box;
        Transform visual, overlay, ring;
        UnitAnimator anim;
        HealthBar bar;
        float height;
        Gait gait;
        bool kicks, harmless;

        // Where the parts are, in this object's space (see SetUpParts). Feet push from where they
        // stand, in their own half of the step cycle; the body sags and drags at the end nothing
        // holds up; the attack comes from the attack part.
        readonly List<(Vector3 pos, float phase)> feet = new();
        Vector3 attackLocal, attackDirLocal = Vector3.forward;
        Vector3 sagDirLocal, bellyLocal;
        float support = 1f;
        bool closingIn;
        // How hard the unit's attacks throw its own body about: attack-part weight (energy) against body mass.
        float heft = 1f, windup;

        BattleUnit target;
        float attackTimer, retargetTimer;
        readonly Dictionary<AbilityDefinition, float> abilityTimers = new();

        float balance = 1f, toppledUntil = -1f, walkPhase, lastFootingTime = -1f, splashTimer;
        readonly RaycastHit[] obstacleHits = new RaycastHit[8];
        Vector3 waypoint;
        float waypointTimer;
        int detourSide;
        float detourUntil;
        bool gettingUp;
        BattleUnit pendingHit;
        float pendingHitTime;

        // Robots: ramming hurts above this change of speed (m/s), by this much per m/s more.
        const float RamThreshold = 1.5f, RamDamage = 6f;
        // Stuck on its back or side this long (after RobotDrive calls it stuck) and it's counted out.
        const float CountOut = 4f;
        RobotDrive drive;
        RobotBrain brain;
        float stuckFor;
        static PhysicsMaterial hull;

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
            if (battle.Database.GetBody(bp.bodyId) is ChassisDefinition chassis) bu.InitRobot(unit, battle, teamColor, chassis);
            else bu.Init(unit, battle, teamColor);
            return bu;
        }

        void Init(UnitInstance unit, BattleManager b, Color teamColor)
        {
            Unit = unit;
            battle = b;
            visual = unit.transform;
            anim = visual.GetComponent<UnitAnimator>();
            IsRanged = unit.Tags.Contains("ranged");
            IsThrower = unit.Tags.Contains("thrower");
            gait = unit.Gait;
            kicks = !unit.HasAttackPart && gait.CanMove;
            harmless = !unit.HasAttackPart && !gait.CanMove;

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
            box.material = Standing;

            body = GetComponent<Rigidbody>();
            body.mass = 1f + Unit.Stats.Get(StatType.MaxHealth) / 100f;
            body.linearDamping = 0.2f;
            body.angularDamping = 1.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // A low centre of mass keeps units planted until something really hits them.
            body.centerOfMass = new Vector3(box.center.x, height * 0.4f, box.center.z);
            body.isKinematic = true; // frozen until the fight starts
            SetUpParts();
            CreateOverlay(teamColor);

            attackTimer = Random.Range(0.1f, 0.5f); // so a line of units doesn't swing in lockstep
            foreach (var ab in unit.Abilities) abilityTimers[ab] = ab.cooldown;
        }

        /// <summary>
        /// Sets up a wheeled robot: a box collider for the hull, weight from its parts, wheels driven
        /// by a RobotDrive whose power comes from the wheel motors, a RobotBrain to steer (switched on
        /// when the fight starts) and its weapons armed.
        /// </summary>
        void InitRobot(UnitInstance unit, BattleManager b, Color teamColor, ChassisDefinition chassis)
        {
            IsRobot = true;
            Unit = unit;
            battle = b;
            visual = unit.transform;
            gait = unit.Gait;

            var hullT = visual.Find("Chassis");
            box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(hullT != null ? hullT.position : visual.position);
            box.size = chassis.size;
            box.material = Hull;
            height = box.center.y + chassis.size.y * 0.5f;
            Radius = Mathf.Clamp(Mathf.Max(chassis.size.x, chassis.size.z) * 0.6f, 0.3f, 1.5f);

            body = GetComponent<Rigidbody>();
            body.mass = Mathf.Max(1f, RobotAssembler.Mass(unit.Source, b.Database));
            body.linearDamping = 0.05f;
            body.angularDamping = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.centerOfMass = box.center + Vector3.down * chassis.size.y * 0.3f;
            body.isKinematic = true; // frozen until the fight starts

            foreach (var w in GetComponentsInChildren<RobotWeapon>()) w.Arm(this);
            drive = gameObject.AddComponent<RobotDrive>();
            float motor = 0f;
            foreach (var a in unit.Source.parts)
                if (b.Database.GetPart(a.partId) is RobotPartDefinition p && p.type == RobotPartType.Wheel) motor += p.motor;
            drive.power = motor / body.mass;
            drive.maxSpeed = Mathf.Max(1f, unit.Stats.Get(StatType.Speed));
            brain = gameObject.AddComponent<RobotBrain>();
            brain.lineUpDistance = Random.Range(1.2f, 2f);
            brain.enabled = false;

            CreateOverlay(teamColor);
            foreach (var ab in unit.Abilities) abilityTimers[ab] = ab.cooldown;
        }

        /// <summary>Hulls slide a little when shoved but don't skate.</summary>
        static PhysicsMaterial Hull => hull ??= new PhysicsMaterial("Robot Hull")
        {
            dynamicFriction = 0.35f, staticFriction = 0.45f, frictionCombine = PhysicsMaterialCombine.Average,
            bounciness = 0.1f,
        };

        void CreateOverlay(Color teamColor)
        {
            // Ring and health bar live outside the body so they stay flat and upright when it tips.
            overlay = new GameObject($"{name} Overlay").transform;
            overlay.SetParent(battle.UnitRoot, false);
            var ringGo = BattleVisuals.Primitive(PrimitiveType.Cylinder, "Team Ring", overlay, BattleVisuals.Unlit(teamColor));
            ring = ringGo.transform;
            ring.localPosition = new Vector3(0f, 0.01f, 0f);
            ring.localScale = new Vector3(Radius * 1.5f, 0.005f, Radius * 1.5f);
            ringGo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bar = HealthBar.Create(overlay, height + 0.35f, teamColor);
            bar.Set(1f);
        }

        /// <summary>
        /// Reads where the parts sit: the feet (walking legs, plus a pair under the body for legs
        /// built into its model), how well they hold the body up and which end hangs, and where the
        /// attack comes from and which way it reaches.
        /// </summary>
        void SetUpParts()
        {
            var mounts = UnitAssembler.Mounts(Unit.Source, battle.Database);
            Vector3 ToLocal(Vector3 bodyPoint) => transform.InverseTransformPoint(visual.TransformPoint(bodyPoint));
            Vector3 ToLocalDir(Vector3 bodyDir) => transform.InverseTransformDirection(visual.TransformDirection(bodyDir));
            var mid = new Vector3(box.center.x, 0f, box.center.z);

            // Feet step in diagonal pairs: left against right, front against back.
            var legs = mounts.FindAll(m => m.walks);
            float midZ = 0f;
            foreach (var m in legs) midZ += ToLocal(m.slot.localPosition).z;
            if (legs.Count > 0) midZ /= legs.Count;
            foreach (var m in legs)
            {
                var p = ToLocal(m.slot.localPosition);
                p.y = 0f;
                float phase = (p.x < mid.x - 0.05f ? Mathf.PI : 0f) + (p.z < midZ - 0.05f ? Mathf.PI : 0f);
                feet.Add((p, phase));
            }
            var bodyDef = battle.Database.GetBody(Unit.Source.bodyId);
            if (bodyDef != null && bodyDef.builtInLegs > 0)
            {
                float side = box.size.x * 0.25f;
                feet.Add((mid + Vector3.left * side, Mathf.PI));
                feet.Add((mid + Vector3.right * side, 0f));
            }

            // Legs bunched at one end hold that end up; the other end sags and drags on the ground.
            if (feet.Count > 0)
            {
                var c = Vector3.zero;
                foreach (var f in feet) c += f.pos;
                var offset = c / feet.Count - mid;
                float hx = Mathf.Max(0.1f, box.size.x * 0.5f), hz = Mathf.Max(0.1f, box.size.z * 0.5f);
                support = Gait.Support(feet.Count, new Vector2(offset.x / hx, offset.z / hz));
                if (offset.magnitude > 0.05f)
                {
                    sagDirLocal = -offset.normalized;
                    bellyLocal = mid + new Vector3(sagDirLocal.x * hx, 0.05f, sagDirLocal.z * hz);
                }
            }

            // The attack part: whatever does this unit's kind of attack, or the front foot for a kick.
            var attackers = mounts.FindAll(m =>
                IsThrower ? m.part.tags.Contains("thrower")
                : IsRanged ? m.part.tags.Contains("ranged")
                : m.part.tags.Contains("melee"));
            if (attackers.Count == 0 && kicks && legs.Count > 0)
            {
                var front = legs[0];
                foreach (var m in legs) if (m.slot.localPosition.z > front.slot.localPosition.z) front = m;
                attackers.Add(front);
            }
            if (attackers.Count == 0)
            {
                attackLocal = new Vector3(box.center.x, height * 0.5f, box.center.z + box.size.z * 0.5f);
                return;
            }
            // Parts pointing different ways (a claw in front, a tail behind): lead with the
            // hardest-hitting one and the parts that reach the same way.
            var lead = attackers[0];
            foreach (var m in attackers) if (AttackOf(m.part) > AttackOf(lead.part)) lead = m;
            attackers.RemoveAll(m => Vector3.Dot(m.reach, lead.reach) < 0.5f);
            int weight = 0;
            foreach (var m in attackers) weight += m.part.energyCost;
            heft = Mathf.Clamp(weight / (2f * body.mass), 0.3f, 2.5f);
            var point = Vector3.zero;
            var dir = Vector3.zero;
            foreach (var m in attackers)
            {
                // The tip: as far along its reach as the part's model goes (a long tail reaches far).
                var root = ToLocal(m.slot.localPosition);
                var reach = ToLocalDir(m.reach);
                float tip = 0.3f;
                var model = visual.Find($"{m.slot.slotId}: {m.part.displayName}");
                if (model != null)
                    foreach (var r in model.GetComponentsInChildren<Renderer>())
                    {
                        var b = r.bounds;
                        for (int i = 0; i < 8; i++)
                        {
                            var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                            tip = Mathf.Max(tip, Vector3.Dot(transform.InverseTransformPoint(corner) - root, reach));
                        }
                    }
                point += root + reach * tip * 0.85f;
                dir += reach;
            }
            attackLocal = point / attackers.Count;
            dir.y = 0f;
            attackDirLocal = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        }

        static float AttackOf(PartDefinition p)
        {
            float a = 0f;
            foreach (var m in p.modifiers) if (m.stat == StatType.Attack && m.op == ModifierOp.Flat) a += m.value;
            return a;
        }

        /// <summary>Distance from the attack part to the target's edge.</summary>
        float AttackGap(BattleUnit t) => Flat(t.transform.position - AttackPoint).magnitude - t.Radius;

        static PhysicsMaterial Slippery => slippery ??= new PhysicsMaterial("Unit")
        {
            dynamicFriction = 0.1f, staticFriction = 0.1f, frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0.05f,
        };

        static PhysicsMaterial Grippy => grippy ??= new PhysicsMaterial("Fallen")
        {
            dynamicFriction = 0.8f, staticFriction = 0.9f, frictionCombine = PhysicsMaterialCombine.Maximum,
        };

        /// <summary>Walkers glide on their feet; a unit with no legs sits on its belly and grips.</summary>
        PhysicsMaterial Standing => gait.CanMove ? Slippery : Grippy;

        void OnDestroy()
        {
            if (overlay != null) Destroy(overlay.gameObject);
        }

        /// <summary>Wakes the body when the battle starts.</summary>
        public void SetSimulated(bool on)
        {
            body.isKinematic = !on;
            if (brain != null) brain.enabled = on && IsAlive;
        }

        float Tilt => Vector3.Angle(transform.up, Vector3.up);
        bool Toppled => Time.time < toppledUntil || gettingUp;
        /// <summary>Touching something it can stand on (the ground, a slope, a crate, another unit) just now.</summary>
        bool Grounded => Time.fixedTime - lastFootingTime < 0.1f;

        void OnCollisionStay(Collision c)
        {
            for (int i = 0; i < c.contactCount; i++)
                if (c.GetContact(i).normal.y > 0.5f) { lastFootingTime = Time.fixedTime; return; }
        }

        // Decisions (per frame)

        void Update()
        {
            if (!IsAlive || battle.Phase != BattlePhase.Fighting) return;
            if (IsRobot)
            {
                RobotUpdate();
                return;
            }
            float dt = Time.deltaTime;
            TickAbilities(dt);

            if (pendingHit != null && Time.time >= pendingHitTime) LandHit();

            retargetTimer -= dt;
            if (target == null || !target.IsAlive || retargetTimer <= 0f)
            {
                target = battle.NearestEnemy(this);
                retargetTimer = RetargetInterval;
            }
            windup = 0f;
            if (target == null || Toppled || harmless) return;

            // Attacks come from the attack part, so it has to be in reach and pointing at the target.
            float gap = AttackGap(target);
            bool facing = Vector3.Dot(AttackDir, Flat(target.transform.position - AttackPoint).normalized) > 0.6f;
            windup = 0f;
            if (gap <= Reach && facing)
            {
                attackTimer -= dt;
                if (balance > 0.15f) windup = Mathf.Clamp01(1f - attackTimer / UnitAnimator.WindupTime(AttackKind));
                if (attackTimer <= 0f && balance > 0.15f)
                {
                    Attack(target);
                    attackTimer = IsThrower ? ThrowInterval : AttackInterval;
                }
                // Anticipation over the last moment before each attack.
                if (balance > 0.15f && anim != null)
                    anim.SetWindup(AttackKind, 1f - attackTimer / UnitAnimator.WindupTime(AttackKind));
            }
            else
            {
                attackTimer = Mathf.Max(attackTimer, 0.25f); // a short wind-up after arriving
            }
        }

        /// <summary>Distance from the attack part to the target's edge at which this unit can attack.</summary>
        float Reach => Mathf.Max(0.35f, Unit.Stats.Get(StatType.Range) - 0.6f);

        AttackKind AttackKind => IsThrower ? AttackKind.Throw : IsRanged ? AttackKind.Spit : AttackKind.Melee;

        void Attack(BattleUnit t)
        {
            if (anim != null) anim.Strike(AttackKind);
            if (IsThrower)
            {
                // Follow-through: the rock's weight yanks the thrower forward and over, high up, so it
                // pitches after the throw and may stumble.
                var origin = ThrowOrigin;
                Boulder.Throw(this, t, Unit.Stats.Get(StatType.Attack), battle.BoulderMesh, battle.BoulderMaterial);
                var throwDir = Flat(t.transform.position - transform.position).normalized;
                body.AddForceAtPosition((throwDir * ThrowYank + Vector3.down) * heft, origin, ForceMode.VelocityChange);
                Tip(throwDir, 6f * heft);
                balance = Mathf.Max(0f, balance - 0.5f * heft);
                return;
            }
            if (IsRanged)
            {
                // Recoil: the mouth kicks back and up, rearing the body away from the shot.
                Projectile.Fire(this, t, Unit.Stats.Get(StatType.Attack), BattleVisuals.Palette.Acid);
                body.AddForceAtPosition(-AttackDir * (1f + SpitKick * heft) + Vector3.up * 0.5f * heft, MuzzlePosition, ForceMode.VelocityChange);
                Tip(-AttackDir, 4f * heft);
                balance = Mathf.Max(0f, balance - 0.3f * heft);
                return;
            }
            // Fling the attack part at the target, dragging the body after it (an off-centre part
            // twists the body into the blow); it lands a moment later if the target is still in reach.
            var dir = Flat(t.transform.position - AttackPoint).normalized;
            body.AddForceAtPosition(dir * LungeSpeed * (0.7f + 0.5f * heft) + Vector3.up * 0.8f, AttackPoint, ForceMode.VelocityChange);
            // Throwing the weight of the swing: the body twists round behind the striking part and pitches into it.
            float twist = Vector3.Cross(Flat(AttackPoint - CenterPosition), dir).y;
            body.AddTorque(Vector3.up * twist * 5f * heft, ForceMode.VelocityChange);
            Tip(dir, 3f * heft);
            balance = Mathf.Max(0f, balance - 0.25f * heft);
            pendingHit = t;
            pendingHitTime = Time.time + HitDelay;
        }

        void LandHit()
        {
            var t = pendingHit;
            pendingHit = null;
            if (!IsAlive || t == null || !t.IsAlive) return;
            var to = Flat(t.transform.position - AttackPoint);
            if (AttackGap(t) > Reach + 0.5f)
            {
                // Whiffed: nothing stops the swing, so the body overshoots and stumbles after it.
                body.AddForceAtPosition(to.normalized * 1.5f * heft, AttackPoint, ForceMode.VelocityChange);
                Tip(to.normalized, 5f * heft);
                balance = Mathf.Max(0f, balance - 0.45f * heft);
                return;
            }
            float damage = Unit.Stats.Get(StatType.Attack) * (kicks ? KickDamage : 1f);
            float knock = Mathf.Min(MaxKnock, (MeleeKnock + damage * MeleeKnockPerDamage) * Mass / t.Mass);
            if (kicks) knock *= KickKnock;
            var point = Vector3.Lerp(t.CenterPosition, AttackPoint, 0.5f);
            t.Hit(damage, to.normalized, knock, point);
            // The blow pushes back too: a heavy target bounces the attacker off it.
            body.AddForceAtPosition(-to.normalized * Mathf.Min(2.5f, knock * 0.35f * t.Mass / Mass), AttackPoint, ForceMode.VelocityChange);
            Tip(-to.normalized, Mathf.Min(3f, knock * 0.5f * t.Mass / Mass));
        }

        /// <summary>Rocks the body over toward a flat direction, as a spin in rad/s; balance pulls it back up.</summary>
        void Tip(Vector3 toward, float spin)
        {
            if (body.isKinematic) return;
            body.AddTorque(Vector3.Cross(Vector3.up, Flat(toward).normalized) * spin, ForceMode.VelocityChange);
        }

        /// <summary>
        /// Deals damage and shoves the unit. Knock is a speed in m/s; it's applied above the centre of
        /// mass so strong hits tip the target over as well as pushing it back. Knock-down hits (boulders)
        /// also flip the unit so it goes over whatever its shape.
        /// </summary>
        public void Hit(float damage, Vector3 dir, float knock, Vector3 point, bool knockDown = false)
        {
            if (!IsAlive) return;
            Unit.TakeDamage(damage);
            bar.Set(Unit.CurrentHealth / Mathf.Max(1f, Unit.Stats.Get(StatType.MaxHealth)));
            if (anim != null) anim.Flinch();

            dir = Flat(dir).normalized;
            if (!body.isKinematic)
            {
                var impulse = (dir + Vector3.up * 0.3f) * knock * body.mass;
                var high = new Vector3(point.x, Mathf.Max(point.y, body.worldCenterOfMass.y + height * 0.25f), point.z);
                body.AddForceAtPosition(impulse, high, ForceMode.Impulse);
                balance = Mathf.Max(0f, balance - knock / (6f * gait.Stability));
                if (knockDown && dir != Vector3.zero)
                {
                    balance = 0f;
                    body.AddTorque(Vector3.Cross(Vector3.up, dir) * 7f, ForceMode.VelocityChange);
                }
            }
            battle.OnHit(point, knock, Team);
            if (!IsAlive) Die(dir * knock);
        }

        /// <summary>Robot targeting: the brain drives at the nearest enemy; a robot stuck upside down gets counted out.</summary>
        void RobotUpdate()
        {
            TickAbilities(Time.deltaTime);
            retargetTimer -= Time.deltaTime;
            if (target == null || !target.IsAlive || retargetTimer <= 0f)
            {
                target = battle.NearestEnemy(this);
                retargetTimer = RetargetInterval;
            }
            brain.SetTarget(target != null ? target.transform : null);
            stuckFor = drive.Stuck ? stuckFor + Time.deltaTime : 0f;
            if (stuckFor > CountOut) Kill();
        }

        /// <summary>Damage without a shove, for robot weapons and rams that push with physics themselves.</summary>
        public void Damage(float damage, Vector3 point, float knock)
        {
            if (!IsAlive) return;
            Unit.TakeDamage(damage);
            bar.Set(Unit.CurrentHealth / Mathf.Max(1f, Unit.Stats.Get(StatType.MaxHealth)));
            battle.OnHit(point, knock, Team);
            if (!IsAlive) Die(Vector3.zero);
        }

        /// <summary>Robots hurt each other by ramming: the harder the crash, the more damage each takes.</summary>
        void OnCollisionEnter(Collision c)
        {
            if (!IsRobot || !IsAlive || !Fighting || c.rigidbody == null) return;
            if (!c.rigidbody.TryGetComponent<BattleUnit>(out var other) || other.Team == Team) return;
            float dv = c.impulse.magnitude / body.mass;
            if (dv < RamThreshold) return;
            Damage((dv - RamThreshold) * RamDamage, c.contactCount > 0 ? c.GetContact(0).point : transform.position, dv);
        }

        void Die(Vector3 push)
        {
            pendingHit = null;
            if (IsRobot)
            {
                // A wreck: no steering, and the wheels stop (some may fly off below).
                brain.enabled = false;
                drive.enabled = false;
            }
            overlay.gameObject.SetActive(false);
            box.material = Grippy;
            body.angularDamping = 0.5f;
            if (anim != null) anim.Freeze();
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
            if (IsRobot)
            {
                if (transform.position.y < -3f) Kill(); // off the edge or down a pit
                return;
            }
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
                balance = Mathf.MoveTowards(balance, 1f, BalanceRecovery * gait.Stability * dt);
                strength = Mathf.Lerp(0.15f, 1f, balance);
            }

            // Stay upright: torque toward world up, damped on the tipping axes. More legs hold harder.
            var w = body.angularVelocity;
            var axis = Vector3.Cross(transform.up, Vector3.up);
            var tipW = w - Vector3.Project(w, Vector3.up);
            body.AddTorque((axis * UprightStrength * gait.Stability - tipW * UprightDamping) * strength, ForceMode.Acceleration);
            if (gettingUp && Grounded) body.AddForce(Vector3.up * 4f, ForceMode.Acceleration); // a little hop helps

            if (Time.time < toppledUntil || gettingUp || target == null || !target.IsAlive || battle.Phase != BattlePhase.Fighting)
                return;

            // Winding up hauls the weapon back (and up, for a throw): pulled from the attack part, it
            // rocks the body back and twists it away from the coming swing.
            if (windup > 0f)
            {
                var pull = -AttackDir * WindupPull + (IsThrower ? Vector3.up * WindupLift : Vector3.zero);
                body.AddForceAtPosition(pull * heft * windup * body.mass, IsThrower ? ThrowOrigin : AttackPoint, ForceMode.Force);
            }

            // Which way to face: on the way the body's front leads, but up close (or rooted to the spot)
            // it hauls itself round until its attack part points at the target.
            var to = Flat(target.transform.position - transform.position);
            float bodyGap = to.magnitude - Radius - target.Radius;
            // A little slack before giving up on closing in, so a shove mid-turn doesn't flip it back.
            closingIn = !gait.CanMove || bodyGap <= Reach + (closingIn ? 1.2f : 0.3f);
            bool close = closingIn;
            var facing = close ? AttackDir : Flat(transform.forward).normalized;
            var aimFrom = close ? AttackPoint : transform.position;
            float yawErr = Vector3.SignedAngle(facing, Flat(target.transform.position - aimFrom), Vector3.up) * Mathf.Deg2Rad;
            float yawAccel = (yawErr * TurnStrength - w.y * TurnDamping) * strength;

            var vel = Flat(body.linearVelocity);
            var arena = battle.Arena;
            float wade = arena != null ? arena.WadeDepth(transform.position) : 0f;
            if (wade > 0f)
            {
                body.AddForce(-vel * WaterDrag, ForceMode.Acceleration);
                splashTimer -= dt;
                if (splashTimer <= 0f && vel.sqrMagnitude > 0.5f)
                {
                    splashTimer = 0.35f;
                    var s = arena.WaterAt(transform.position.x, transform.position.z);
                    BattleEffects.Spark(new Vector3(transform.position.x, s.waterLevel, transform.position.z),
                                        0.35f + Radius * 0.4f, Color.Lerp(s.waterColor, Color.white, 0.5f));
                }
            }
            if (!gait.CanMove)
            {
                // No legs: stays put, only sliding as far as hits shove it, and shuffles round slowly.
                body.AddTorque(Vector3.up * yawAccel * 0.5f, ForceMode.Acceleration);
                if (Grounded) body.AddForce(-vel * 3f, ForceMode.Acceleration);
                return;
            }

            // Walk toward the target; once close, only shuffle the attack part into reach.
            float speed = Unit.Stats.Get(StatType.Speed) * (arena != null ? arena.SpeedFactor(transform.position) : 1f);
            Vector3 desired;
            if (!close) desired = Flat(Waypoint(arena) - transform.position).normalized * speed;
            else if (AttackGap(target) > Reach) desired = Flat(target.transform.position - AttackPoint).normalized * speed * 0.4f;
            else desired = Vector3.zero;
            desired = SteerAroundObstacles(desired);
            if (arena != null) desired = arena.SteerAroundHoles(transform.position, desired, Radius);

            bool busy = desired != Vector3.zero || Mathf.Abs(yawErr) > 0.15f;
            if (busy) walkPhase += dt * (5f + speed);
            if (Grounded) PushWithFeet(Vector3.ClampMagnitude((desired - vel) * 6f, MoveAccel) * strength, yawAccel, busy);
            DragBelly();

            if (desired != Vector3.zero)
            {
                // A TABS-ish waddle: rock side to side while walking. One leg wobbles, a limp lurches.
                float rock = 6f * (gait.Legs == 1 ? 1.8f : 1f) * (1f + Mathf.Abs(gait.Lean));
                body.AddTorque(transform.forward * Mathf.Sin(walkPhase) * rock * strength, ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Moves and turns the body by pushing from each foot where it stands, so a body is dragged
        /// along by its legs rather than gliding. Only feet in their half of the step cycle push,
        /// so one leg lurches and many legs scuttle smoothly. Part of the turning comes from the feet
        /// shuffling on the spot; the rest from pushing sideways where they stand, so a leg at the
        /// tail swings the body round its front, and lopsided legs pull the walk into a curve.
        /// </summary>
        void PushWithFeet(Vector3 accel, float yawAccel, bool stepping)
        {
            if (feet.Count == 0) return;
            var com = body.worldCenterOfMass;
            int n = 0;
            float sumR2 = 0f;
            for (int i = 0; i < feet.Count; i++)
            {
                if (stepping && Mathf.Sin(walkPhase + feet[i].phase) < -0.3f) continue; // foot in the air
                n++;
                sumR2 += Flat(transform.TransformPoint(feet[i].pos) - com).sqrMagnitude;
            }
            if (n == 0) return;

            body.AddTorque(Vector3.up * yawAccel * (1f - FootTurnShare), ForceMode.Acceleration);
            // Feet at r_i pushing along up × r_i with strength k give a torque of k·Σ|r_i|² about up.
            float iy = Mathf.Max(0.01f, body.inertiaTensor.y);
            float k = sumR2 > 0.02f ? yawAccel * FootTurnShare * iy / (body.mass * sumR2) : 0f;
            if (sumR2 <= 0.02f) body.AddTorque(Vector3.up * yawAccel * FootTurnShare, ForceMode.Acceleration);
            for (int i = 0; i < feet.Count; i++)
            {
                if (stepping && Mathf.Sin(walkPhase + feet[i].phase) < -0.3f) continue;
                var p = transform.TransformPoint(feet[i].pos);
                p.y = com.y; // push level with the centre of mass so feet don't flip the body over
                var r = Flat(p - com);
                var a = Vector3.ClampMagnitude(accel / n + Vector3.Cross(Vector3.up, r) * k, MaxFootAccel);
                body.AddForceAtPosition(a * body.mass, p, ForceMode.Force);
            }
        }
        /// <summary>Where to walk toward the target: straight at it, or round whatever is in the way.</summary>
        Vector3 Waypoint(Arena arena)
        {
            if (arena == null || arena.Nav == null) return target.transform.position;
            waypointTimer -= Time.fixedDeltaTime;
            if (waypointTimer <= 0f)
            {
                waypointTimer = 0.4f;
                waypoint = arena.Nav.NextWaypoint(transform.position, target.transform.position);
            }
            // Close to an intermediate waypoint: look again for the next one.
            if (Flat(waypoint - transform.position).sqrMagnitude < 0.2f) waypointTimer = 0f;
            return waypoint;
        }

        /// <summary>
        /// Slides along fixed obstacles (log piles, rocks, crate stacks) instead of walking into them,
        /// keeping to one side until the way ahead is clear. Units and loose props are just pushed.
        /// </summary>
        Vector3 SteerAroundObstacles(Vector3 desired)
        {
            if (desired == Vector3.zero) return desired;
            var dir = desired.normalized;
            var from = new Vector3(transform.position.x, transform.position.y + Mathf.Min(height * 0.5f, 0.45f), transform.position.z);
            int n = Physics.SphereCastNonAlloc(from, Radius * 0.7f, dir, obstacleHits, 1.3f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var h = obstacleHits[i];
                var rb = h.collider.attachedRigidbody;
                if (rb != null && rb != body && !rb.isKinematic) continue; // units, loose crates: shove them
                if (rb == body || h.normal.y > 0.6f || h.distance <= 0f) continue; // self, ground, already touching
                if (h.distance < nearest) { nearest = h.distance; normal = Flat(h.normal).normalized; }
            }
            if (normal == Vector3.zero)
            {
                if (Time.time > detourUntil) detourSide = 0;
                return desired;
            }
            // Pick a side once (the one that turns away least) and stick with it for a moment.
            var tangent = Vector3.Cross(Vector3.up, normal);
            if (detourSide == 0) detourSide = Vector3.Dot(tangent, dir) >= 0f ? 1 : -1;
            detourUntil = Time.time + 0.8f;
            float block = 1f - Mathf.Clamp01(nearest / 1.3f);
            var slide = (tangent * detourSide + normal * 0.25f).normalized;
            return Vector3.Lerp(dir, slide, 0.35f + 0.65f * block).normalized * desired.magnitude;
        }


        /// <summary>The end no legs hold up scrapes along the ground, so the legs have to drag it.</summary>
        void DragBelly()
        {
            if (!Grounded || support >= 0.95f || sagDirLocal == Vector3.zero) return;
            var p = transform.TransformPoint(bellyLocal);
            var v = Flat(body.GetPointVelocity(p));
            body.AddForceAtPosition(-v * BellyDrag * (1f - support) * body.mass, p, ForceMode.Force);
        }

        void EndTopple()
        {
            gettingUp = false;
            toppledUntil = -1f;
            balance = 0.5f;
            box.material = Standing;
        }

        void LateUpdate()
        {
            if (overlay != null && overlay.gameObject.activeSelf)
            {
                var p = transform.position;
                var arena = battle.Arena;
                float ground = arena != null ? arena.HeightAt(p.x, p.z) : 0f;
                overlay.SetPositionAndRotation(new Vector3(p.x, Mathf.Max(ground, p.y), p.z), Quaternion.identity);
                if (arena != null)
                {
                    var s = arena.WaterAt(p.x, p.z);
                    ring.position = new Vector3(p.x, Mathf.Max(ground, s != null ? s.waterLevel : ground) + 0.01f, p.z);
                    ring.rotation = Quaternion.FromToRotation(Vector3.up, arena.NormalAt(p.x, p.z));
                }
            }
            // Feed the animator: walk from the body's velocity, look at the target, flail when down.
            if (anim != null && anim.enabled)
            {
                anim.SetMotion(body.isKinematic ? Vector3.zero : body.linearVelocity, Grounded || body.isKinematic);
                anim.SetLookTarget(target != null && target.IsAlive ? target.CenterPosition : null);
                anim.Flailing = Toppled && Time.time < toppledUntil;
            }
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
                // On the ground under the unit, lying along the slope there.
