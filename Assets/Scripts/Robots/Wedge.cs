using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>A ramp on the nose that scoops enemies up. No moving parts: physics does the work.</summary>
    public class Wedge : RobotWeapon
    {
        Transform plate;
        Vector3 size;

        public void Setup(RobotPartDefinition d, Transform plateT, Vector3 plateSize)
        {
            def = d;
            plate = plateT;
            size = plateSize;
        }

        public override void Arm(BattleUnit unit)
        {
            base.Arm(unit);
            AddBox(plate, Vector3.zero, size);
        }
    }
}
