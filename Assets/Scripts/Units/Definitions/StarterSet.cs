using System;
using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    [Serializable]
    public class StarterPart
    {
        public PartDefinition part;
        public int count = 1;
    }

    /// <summary>The fixed collection every new run starts from.</summary>
    [CreateAssetMenu(menuName = "Units/Starter Set")]
    public class StarterSet : ScriptableObject
    {
        public List<BodyDefinition> bodies = new();
        public List<StarterPart> parts = new();
        public List<BuffDefinition> buffs = new();
    }
}
