using System.Collections.Generic;
using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// Base for robot weapons. A weapon sits on its mount facing +Z, idles in the builder, and once
    /// armed for battle watches a hit zone in front of it for enemy robots, striking them with damage
    /// and a physical shove. Shoves are speeds for a 10 kg robot, so heavy robots get thrown less.
    /// </summary>
    public abstract class RobotWeapon : MonoBehaviour
    {
        protected const float ReferenceMass = 10f;

        protected RobotPartDefinition def;
        protected BattleUnit owner;
        protected float cooldownLeft;

        static readonly Collider[] Overlaps = new Collider[16];
        static readonly List<BattleUnit> Found = new();

        protected Rigidbody Body => owner != null ? owner.Body : null;
        /// <summary>The robot is fighting and alive: weapons only work then.</summary>
        protected bool Live => owner != null && owner.IsAlive && owner.Fighting;

        /// <summary>Called when the robot is built for battle: adds colliders and starts the weapon.</summary>
        public virtual void Arm(BattleUnit unit)
        {
            owner = unit;
            cooldownLeft = Random.Range(0.2f, 0.6f);
        }

        protected virtual void Update()
        {
            if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;
        }

        /// <summary>Enemy robots overlapping a box given in this weapon's space.</summary>
        protected List<BattleUnit> EnemiesIn(Vector3 center, Vector3 halfExtents)
        {
            Found.Clear();
            if (owner == null) return Found;
            int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(center), halfExtents, Overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var rb = Overlaps[i].attachedRigidbody;
                if (rb == null || rb == Body || !rb.TryGetComponent<BattleUnit>(out var u)) continue;
                if (!u.IsAlive || u.Team == owner.Team || Found.Contains(u)) continue;
                Found.Add(u);
            }
            return Found;
        }

        /// <summary>Damages a target and throws it: `push` is a speed for a 10 kg robot, applied at `point`.</summary>
        protected void Strike(BattleUnit target, float damage, Vector3 push, Vector3 point)
        {
            if (target.Body != null && !target.Body.isKinematic)
                target.Body.AddForceAtPosition(push * ReferenceMass, point, ForceMode.Impulse);
            target.Damage(damage, point, push.magnitude);
        }

        /// <summary>Kicks the robot carrying this weapon, e.g. a spinner bouncing off what it hit.</summary>
        protected void Recoil(Vector3 push, Vector3 point)
        {
            if (Body != null && !Body.isKinematic) Body.AddForceAtPosition(push * ReferenceMass, point, ForceMode.Impulse);
        }

        protected static BoxCollider AddBox(Transform t, Vector3 center, Vector3 size)
        {
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            return c;
        }
    }
}
