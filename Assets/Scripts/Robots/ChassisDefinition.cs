using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// A robot body: a box hull with mount points. Its slots are the usual body slots, read as robot
    /// mounts: Head is the front, Tail the rear, Back the top, Arm the sides and Leg the wheel wells.
    /// It has no model; RobotVisuals builds the hull from its size and colour.
    /// </summary>
    [CreateAssetMenu(menuName = "Robots/Chassis")]
    public class ChassisDefinition : Units.BodyDefinition
    {
        [Tooltip("Hull size in metres (width, height, length).")]
        public Vector3 size = new(0.8f, 0.3f, 1f);
        [Tooltip("Hull mass in kg, before parts.")]
        public float mass = 10f;
        public Color color = new(0.55f, 0.52f, 0.47f);
    }
}
