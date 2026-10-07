using System;
using System.Collections.Generic;

namespace RougeLike.Units
{
    [Serializable]
    public class SlotAssignment
    {
        public string slotId;
        public string partId;
    }

    /// <summary>A saved unit design. Stores ids only so saves stay small and survive asset tweaks.</summary>
    [Serializable]
    public class UnitBlueprint
    {
        public string name;
        public string bodyId;
        public List<SlotAssignment> parts = new();

        public string GetPartIn(string slotId) => parts.Find(p => p.slotId == slotId)?.partId;

        public void SetPart(string slotId, string partId)
        {
            parts.RemoveAll(p => p.slotId == slotId);
            if (!string.IsNullOrEmpty(partId))
                parts.Add(new SlotAssignment { slotId = slotId, partId = partId });
        }
    }
}
