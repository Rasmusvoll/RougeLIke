using System;
using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    [Serializable]
    public class SlotDefinition
    {
        [Tooltip("Stable id used by blueprints, e.g. arm_left.")]
        public string slotId;
        public SlotType type;
        [Tooltip("Attach point relative to the body prefab root.")]
        public Vector3 localPosition;
        [Tooltip("Flip the part across X. Parts are modelled for the right side, so left slots set this.")]
        public bool mirror;
    }

    [CreateAssetMenu(menuName = "Units/Body")]
    public class BodyDefinition : ContentDefinition
    {
        public GameObject prefab;
        public List<StatValue> baseStats = new();
        public List<SlotDefinition> slots = new();
        [Tooltip("Budget shared by all attached parts.")]
        public int energy = 10;
        public List<AbilityDefinition> innateAbilities = new();

        public SlotDefinition GetSlot(string slotId) => slots.Find(s => s.slotId == slotId);
    }
}
