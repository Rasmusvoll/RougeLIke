using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>Flat armour that adds bulk to the hull. Its health and defence come from the part's stats.</summary>
    public class ArmorPlate : RobotWeapon
    {
        Vector3 center, size;

        public void Setup(Vector3 c, Vector3 s)
        {
            center = c;
            size = s;
        }

        public override void Arm(BattleUnit unit)
        {
            base.Arm(unit);
            AddBox(transform, center, size);
        }
    }
}
