using System.Collections.Generic;
using RougeLike.Units;
using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>
    /// Drives one unit on the battlefield: walks to the nearest enemy, attacks when in reach, and plays
    /// simple procedural animation (waddle, lunge, flinch, topple). Stats come from the UnitInstance
    /// that UnitAssembler builds as this object's child.
    /// </summary>
    public class BattleUnit : MonoBehaviour
    {
        const float AttackInterval = 1f;
        const float RetargetInterval = 0.5f;
        const float TurnSpeed = 540f;

        public UnitInstance Unit { get; private set; }
        public Team Team => Unit.Team;
        public bool IsAlive => Unit != null && Unit.IsAlive;
        public bool IsRanged { get; private set; }
        public float Radius { get; private set; }
        /// <summary>Index into the player's blueprints, or -1 for enemies.</summary>
        public int BlueprintIndex { get; set; } = -1;

        public Vector3 CenterPosition => transform.position + Vector3.up * height * 0.5f;
        public Vector3 MuzzlePosition => transform.position + transform.forward * Radius + Vector3.up * height * 0.6f;

        BattleManager battle;
        Transform visual;
        Vector3 visualPos, visualScale;
        Quaternion visualRot;
        HealthBar bar;
        GameObject ring;
        float height;

        BattleUnit target;
        float attackTimer, retargetTimer;
        readonly Dictionary<AbilityDefinition, float> abilityTimers = new();

        // Animation state.
        float walkPhase, walkBlend, lunge, flinch, deathTime = -1f;
        Vector3 flinchDir;

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
            var bu = go.AddComponent<BattleUnit>();
            bu.Init(unit, battle, teamColor);
            return bu;
        }

        void Init(UnitInstance unit, BattleManager b, Color teamColor)
        {
            Unit = unit;
            battle = b;
            visual = unit.transform;
            visualPos = visual.localPosition;
            visualRot = visual.localRotation;
            visualScale = visual.localScale;
            IsRanged = unit.Tags.Contains("ranged");

            // Size from the model so big bodies keep their distance and bars sit above the head.
            var renderers = visual.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.one);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.3f, 1.5f);
            height = Mathf.Max(bounds.max.y - transform.position.y, 0.5f);

            ring = BattleVisuals.Primitive(PrimitiveType.Cylinder, "Team Ring", transform, BattleVisuals.Unlit(teamColor));
            ring.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            ring.transform.localScale = new Vector3(Radius * 1.5f, 0.005f, Radius * 1.5f);
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            bar = HealthBar.Create(transform, height + 0.35f, teamColor);
            bar.Set(1f);

            attackTimer = Random.Range(0.1f, 0.5f); // so a line of units doesn't swing in lockstep
            foreach (var ab in unit.Abilities) abilityTimers[ab] = ab.cooldown;
        }

        void Update()
        {
            if (!IsAlive || battle.Phase != BattlePhase.Fighting)
            {
                walkBlend = Mathf.MoveTowards(walkBlend, 0f, Time.deltaTime * 4f);
                return;
            }
            float dt = Time.deltaTime;
            TickAbilities(dt);

            retargetTimer -= dt;
            if (target == null || !target.IsAlive || retargetTimer <= 0f)
            {
                target = battle.NearestEnemy(this);
                retargetTimer = RetargetInterval;
            }
            if (target == null) { walkBlend = Mathf.MoveTowards(walkBlend, 0f, dt * 4f); return; }

            var to = target.transform.position - transform.position;
            to.y = 0f;
            float gap = to.magnitude - Radius - target.Radius;
            Face(to, dt);

            if (gap > Reach)
            {
                float speed = Unit.Stats.Get(StatType.Speed);
                transform.position += to.normalized * Mathf.Min(speed * dt, gap);
                walkPhase += dt * (6f + speed * 1.5f);
                walkBlend = Mathf.MoveTowards(walkBlend, 1f, dt * 4f);
                attackTimer = Mathf.Max(attackTimer, 0.25f); // a short wind-up after arriving
            }
            else
            {
                walkBlend = Mathf.MoveTowards(walkBlend, 0f, dt * 4f);
                attackTimer -= dt;
                if (attackTimer <= 0f)
                {
                    Attack(target);
                    attackTimer = AttackInterval;
                }
            }
        }

        /// <summary>Distance between body edges at which this unit can attack.</summary>
        float Reach => Mathf.Max(0.3f, Unit.Stats.Get(StatType.Range) - 0.6f);

        void Face(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), TurnSpeed * dt);
        }

        void Attack(BattleUnit t)
        {
            float damage = Unit.Stats.Get(StatType.Attack);
            lunge = 1f;
            if (IsRanged) Projectile.Fire(this, t, damage, new Color(0.55f, 1f, 0.3f));
            else t.Hit(damage, (t.transform.position - transform.position).normalized);
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

        public void Hit(float damage, Vector3 fromDir)
        {
            if (!IsAlive) return;
            Unit.TakeDamage(damage);
            flinch = 1f;
            flinchDir = new Vector3(fromDir.x, 0f, fromDir.z).normalized;
            transform.position += flinchDir * 0.12f; // a little knockback
            bar.Set(Unit.CurrentHealth / Mathf.Max(1f, Unit.Stats.Get(StatType.MaxHealth)));
            if (!IsAlive) Die();
        }

        void Die()
        {
            deathTime = Time.time;
            bar.gameObject.SetActive(false);
            ring.SetActive(false);
            battle.OnUnitDied(this);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            lunge = Mathf.MoveTowards(lunge, 0f, dt * 4f);
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 5f);

            if (deathTime >= 0f)
            {
                // Topple sideways, lie there for a bit, then sink out of sight.
                float t = Time.time - deathTime;
                float fall = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                visual.localRotation = Quaternion.Euler(0f, 0f, 90f * fall) * visualRot;
                visual.localPosition = visualPos + new Vector3(0f, -Mathf.Max(0f, t - 2.5f) * 0.5f, 0f);
                if (t > 4.5f) gameObject.SetActive(false);
                return;
            }

            // Waddle while walking, lunge forward on attack, rock back when hit.
            float s = Mathf.Sin(walkPhase);
            float lungeCurve = Mathf.Sin(lunge * Mathf.PI);
            var localFlinch = transform.InverseTransformDirection(flinchDir) * flinch;
            visual.localPosition = visualPos + new Vector3(0f, Mathf.Abs(s) * 0.08f * walkBlend, lungeCurve * (IsRanged ? -0.1f : 0.3f));
            visual.localRotation = Quaternion.Euler(lungeCurve * 12f + localFlinch.z * 18f, 0f, s * 7f * walkBlend - localFlinch.x * 18f) * visualRot;
            float squash = 1f - flinch * 0.12f;
            visual.localScale = Vector3.Scale(visualScale, new Vector3(1f / squash, squash, 1f / squash));
        }
    }
}
