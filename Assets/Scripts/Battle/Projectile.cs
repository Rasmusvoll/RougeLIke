using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>A glob fired by a ranged unit. Homes on its target and deals damage on arrival.</summary>
    public class Projectile : MonoBehaviour
    {
        const float Speed = 11f;

        BattleUnit source, target;
        float damage;
        Vector3 lastTargetPos;

        public static void Fire(BattleUnit source, BattleUnit target, float damage, Color color)
        {
            var go = BattleVisuals.Primitive(PrimitiveType.Sphere, "Projectile", null, BattleVisuals.Unlit(color));
            go.transform.position = source.MuzzlePosition;
            go.transform.localScale = Vector3.one * 0.22f;
            var p = go.AddComponent<Projectile>();
            p.source = source;
            p.target = target;
            p.damage = damage;
            p.lastTargetPos = target.CenterPosition;
        }

        void Update()
        {
            if (target != null && target.IsAlive) lastTargetPos = target.CenterPosition;
            transform.position = Vector3.MoveTowards(transform.position, lastTargetPos, Speed * Time.deltaTime);
            if ((transform.position - lastTargetPos).sqrMagnitude > 0.01f) return;
            if (target != null && target.IsAlive)
                target.Hit(damage, (lastTargetPos - (source != null ? source.transform.position : transform.position)).normalized);
            Destroy(gameObject);
        }
    }
}
