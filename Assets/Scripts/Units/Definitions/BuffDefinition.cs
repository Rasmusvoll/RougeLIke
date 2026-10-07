using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    [CreateAssetMenu(menuName = "Units/Buff")]
    public class BuffDefinition : ContentDefinition
    {
        public List<StatModifier> modifiers = new();
        [Tooltip("Empty = applies to all units. Otherwise only units with a part carrying this tag.")]
        public string requiredTag;
    }
}
