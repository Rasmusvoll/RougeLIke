using RougeLike.Units;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// Builds a robot's model from a blueprint whose body is a ChassisDefinition: the hull, then each
    /// part on its slot, facing out of it. UnitAssembler hands robot blueprints here, so the builder
    /// preview and the battle both get robots through the same calls they use for creatures.
    /// </summary>
    public static class RobotAssembler
    {
        // Wheels settle this far into their suspension under the robot's weight (see RobotDrive).
        const float RestSag = 0.03f;

        public static bool IsRobot(UnitBlueprint bp, ContentDatabase db) => db.GetBody(bp?.bodyId) is ChassisDefinition;

        /// <summary>
        /// The robot's model, stood so its wheels (or its hull, with none) rest on y = 0 of the parent.
        /// Parts are named "slot: Part" like creature parts, so battle damage can knock them off.
        /// </summary>
        public static GameObject SpawnVisual(UnitBlueprint bp, ChassisDefinition chassis, ContentDatabase db, Transform parent)
        {
            var root = new GameObject(string.IsNullOrEmpty(bp.name) ? chassis.displayName : bp.name);
            root.transform.SetParent(parent, false);
            RobotVisuals.Chassis(chassis, root.transform);

            float lowest = -chassis.size.y * 0.5f;
            bool anyWheel = false;
            foreach (var a in bp.parts)
            {
                var slot = chassis.GetSlot(a.slotId);
                if (slot == null || db.GetPart(a.partId) is not RobotPartDefinition part) continue;
                var mount = new GameObject($"{a.slotId}: {part.displayName}").transform;
                mount.SetParent(root.transform, false);
                mount.localPosition = slot.localPosition;
                mount.localRotation = MountRotation(part, slot);
                RobotVisuals.Part(part, mount);
                if (part.type == RobotPartType.Wheel)
                {
                    float bottom = slot.localPosition.y - RobotVisuals.WheelTravel + RestSag - part.wheelRadius;
                    lowest = anyWheel ? Mathf.Min(lowest, bottom) : bottom;
                    anyWheel = true;
                }
            }
            root.transform.localPosition += Vector3.up * -lowest;
            return root;
        }

        /// <summary>
        /// Which way a part faces on a slot: out of the front, back or side. Wheels always roll
        /// forward, top parts face forward, and armour on top lies flat.
        /// </summary>
        public static Quaternion MountRotation(RobotPartDefinition part, SlotDefinition slot)
        {
            if (part.type == RobotPartType.Wheel) return Quaternion.identity;
            switch (slot.type)
            {
                case SlotType.Tail: return Quaternion.Euler(0f, 180f, 0f);
                case SlotType.Arm:
                case SlotType.Leg: return Quaternion.Euler(0f, slot.localPosition.x < 0f ? -90f : 90f, 0f);
                case SlotType.Back: return part.type == RobotPartType.Armor ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
                default: return Quaternion.identity;
            }
        }

        /// <summary>Total weight: hull plus parts.</summary>
        public static float Mass(UnitBlueprint bp, ContentDatabase db)
        {
            if (db.GetBody(bp?.bodyId) is not ChassisDefinition chassis) return 0f;
            float m = chassis.mass;
            foreach (var a in bp.parts)
                if (db.GetPart(a.partId) is RobotPartDefinition p) m += p.mass;
            return m;
        }

        /// <summary>For the builder, in place of the creature walk description. Null for creatures.</summary>
        public static string DescribeMovement(UnitBlueprint bp, ContentDatabase db)
        {
            if (!IsRobot(bp, db)) return null;
            var chassis = (ChassisDefinition)db.GetBody(bp.bodyId);
            int wheels = 0, left = 0, right = 0;
            foreach (var a in bp.parts)
            {
                if (db.GetPart(a.partId) is not RobotPartDefinition p || p.type != RobotPartType.Wheel) continue;
                var slot = chassis.GetSlot(a.slotId);
                if (slot == null || slot.type == SlotType.Back) continue;
                wheels++;
                if (slot.localPosition.x < -0.05f) left++;
                else if (slot.localPosition.x > 0.05f) right++;
            }
            if (wheels == 0) return "Can't move: add wheels";
            if (left == 0 || right == 0) return $"Drives in circles on {wheels} wheel{(wheels == 1 ? "" : "s")}";
            return $"Drives on {wheels} wheels · {Mass(bp, db):0} kg";
        }
    }
}
