using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace RougeLike.Units
{
    [CreateAssetMenu(menuName = "Units/Part")]
    public class PartDefinition : ContentDefinition
    {
        public GameObject prefab;
        [Tooltip("What the part is. Any part fits any slot; the kind decides how it's mounted and animated there.")]
        [FormerlySerializedAs("fitsSlot")]
        public SlotType kind;
        [Tooltip("Ground speed this part gives the unit, in m/s. Above zero makes it a locomotion part (legs; later fins or wings). " +
                 "A unit with no locomotion part can't move.")]
        public float stride;
        [Tooltip("Energy this part uses from the body's budget. Stronger parts cost more.")]
        public int energyCost = 1;
        public List<StatModifier> modifiers = new();
        public List<AbilityDefinition> abilities = new();
        [Tooltip("Free-form tags such as melee or armored, used by buffs and synergies. melee, ranged and thrower parts give the unit its attack.")]
        public List<string> tags = new();

        public bool IsLocomotion => stride > 0f;
    }
}
