using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>A flamethrower: burns everything in a cone in front, a little at a time. No shove.</summary>
    public class Flamethrower : RobotWeapon
    {
        Transform flames;
        float tick;
        static readonly Vector3 ZoneCenter = new(0f, 0.02f, 0.8f), ZoneHalf = new(0.32f, 0.3f, 0.55f);

        public void Setup(RobotPartDefinition d, Transform f)
        {
            def = d;
            flames = f;
        }

        protected override void Update()
        {
            base.Update();
            bool firing = Live && EnemiesIn(ZoneCenter, ZoneHalf).Count > 0;
            if (flames.gameObject.activeSelf != firing) flames.gameObject.SetActive(firing);
            if (firing)
            {
                // Flicker.
                for (int i = 0; i < flames.childCount; i++)
                    flames.GetChild(i).localScale = Vector3.one * (0.12f + i * 0.06f) * Random.Range(0.75f, 1.25f);
                tick -= Time.deltaTime;
                if (tick <= 0f)
                {
                    tick = 0.25f;
                    foreach (var t in EnemiesIn(ZoneCenter, ZoneHalf))
                        Strike(t, def.damage * 0.25f, Vector3.zero, t.CenterPosition);
                }
            }
        }
    }
}
