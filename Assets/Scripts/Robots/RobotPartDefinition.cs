using UnityEngine;

namespace RougeLike.Robots
{
    public enum RobotPartType { Wheel, Wedge, Flipper, Spinner, Drum, Hammer, Flamethrower, Armor, SelfRighter }

    /// <summary>
    /// A robot part: wheels, a weapon or armour. Like creature parts it fits any slot and spends the
    /// chassis's energy; RobotVisuals builds its look and RobotWeapons gives weapons their behaviour.
    /// </summary>
    [CreateAssetMenu(menuName = "Robots/Part")]
    public class RobotPartDefinition : Units.PartDefinition
    {
        public RobotPartType type;
        [Tooltip("Adds to the robot's weight. Heavy robots accelerate slower but are harder to shove.")]
        public float mass = 1f;
        [Tooltip("Paint colour. Leave fully transparent for bare steel.")]
        public Color color = new(0f, 0f, 0f, 0f);

        [Header("Wheels")]
        public float wheelRadius = 0.2f;
        [Tooltip("Sideways grip as a friction coefficient.")]
        public float grip = 1.2f;
        [Tooltip("Motor push in newtons.")]
        public float motor = 40f;

        [Header("Weapons")]
        [Tooltip("Damage per hit (per second for a flamethrower).")]
        public float damage = 10f;
        [Tooltip("How hard a hit throws its target, as the speed in m/s it gives a 10 kg robot.")]
        public float force = 4f;
        [Tooltip("Seconds between hits, or for a spinner, seconds to spin back up to full speed.")]
        public float cooldown = 1.5f;
        [Tooltip("How far in front of its mount the weapon reaches, in metres.")]
        public float reach = 0.6f;

        public string Category => type switch
        {
            RobotPartType.Wheel => "Wheel",
            RobotPartType.Armor => "Armour",
            RobotPartType.SelfRighter => "Utility",
            _ => "Weapon",
        };

        public bool IsWeapon => Category == "Weapon";
    }
}
