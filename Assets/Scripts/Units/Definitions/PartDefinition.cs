using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    [CreateAssetMenu(menuName = "Units/Part")]
    public class PartDefinition : ContentDefinition
    {
        public GameObject prefab;
        public SlotType fitsSlot;
        [Tooltip("Energy this part uses from the body's budget. Stronger parts cost more.")]
        public int energyCost = 1;
        public List<StatModifier> modifiers = new();
        public List<AbilityDefinition> abilities = new();
        [Tooltip("Free-form tags such as melee or armored, used by buffs and synergies.")]
        public List<string> tags = new();
    }
}
