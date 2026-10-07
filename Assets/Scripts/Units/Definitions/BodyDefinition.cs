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
        [Tooltip("Where on the body the slot sits. Any part fits; this decides which way a part faces when mounted here.")]
        public SlotType type;
        [Tooltip("Attach point relative to the body prefab root.")]
        public Vector3 localPosition;
        [Tooltip("Flip the part across X. Parts are modelled for the right side; slots left of centre are mirrored anyway.")]
        public bool mirror;

        public bool Mirrored => mirror || localPosition.x < -0.01f;
    }

    [CreateAssetMenu(menuName = "Units/Body")]
    public class BodyDefinition : ContentDefinition
    {
        public GameObject prefab;
        public List<StatValue> baseStats = new();
        public List<SlotDefinition> slots = new();
        [Tooltip("Budget shared by all attached parts.")]
        public int energy = 10;
        [Tooltip("Multiplies the speed its legs give. Heavy bodies are carried slower.")]
        public float moveScale = 1f;
        [Tooltip("Legs modelled into the body itself, like the Brute's stubby feet. They count as locomotion, so the body can walk without leg parts.")]
        public int builtInLegs;
        [Tooltip("Speed each built-in leg gives, like a leg part's stride.")]
        public float builtInStride = 2f;
        public List<AbilityDefinition> innateAbilities = new();

        public SlotDefinition GetSlot(string slotId) => slots.Find(s => s.slotId == slotId);
    }
}
