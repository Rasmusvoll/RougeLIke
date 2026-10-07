using System;
using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    [Serializable]
    public class PartStack
    {
        public string partId;
        public int count;
    }

    /// <summary>
    /// Everything the player owns during one run. Resets to a StarterSet when a new run begins.
    /// Parts are consumables: equipping one uses up a copy, and replacing or removing it destroys it.
    /// Uses lists rather than dictionaries so JsonUtility can save it.
    /// </summary>
    [Serializable]
    public class PlayerCollection
    {
        public List<string> bodyIds = new();
        public List<PartStack> parts = new();        // unequipped copies
        public List<string> runBuffIds = new();
        public List<UnitBlueprint> blueprints = new();

        public static PlayerCollection FromStarter(StarterSet starter)
        {
            var c = new PlayerCollection();
            if (starter == null) return c;
            foreach (var b in starter.bodies)
                if (b != null) c.AddBody(b.id);
            foreach (var p in starter.parts)
                if (p != null && p.part != null) c.AddPart(p.part.id, p.count);
            foreach (var b in starter.buffs)
                if (b != null) c.runBuffIds.Add(b.id);
            return c;
        }

        // Bodies

        public bool OwnsBody(string bodyId) => bodyIds.Contains(bodyId);

        public void AddBody(string bodyId)
        {
            if (!string.IsNullOrEmpty(bodyId) && !OwnsBody(bodyId)) bodyIds.Add(bodyId);
        }

        // Parts

        public int GetPartCount(string partId) => parts.Find(p => p.partId == partId)?.count ?? 0;

        public void AddPart(string partId, int count = 1)
        {
            if (string.IsNullOrEmpty(partId) || count <= 0) return;
            var stack = parts.Find(p => p.partId == partId);
            if (stack == null) parts.Add(new PartStack { partId = partId, count = count });
            else stack.count += count;
        }

        public bool RemovePart(string partId, int count = 1)
        {
            var stack = parts.Find(p => p.partId == partId);
            if (stack == null || stack.count < count) return false;
            stack.count -= count;
            if (stack.count == 0) parts.Remove(stack);
            return true;
        }

        // Rewards

        public void Grant(RewardEntry r)
        {
            if (r == null || r.content == null) return;
            switch (r.kind)
            {
                case RewardKind.Body: AddBody(r.content.id); break;
                case RewardKind.Part: AddPart(r.content.id); break;
                case RewardKind.Buff: runBuffIds.Add(r.content.id); break;
            }
        }

        // Equipping

        /// <summary>
        /// Puts one copy of a part into a blueprint slot, consuming it from the collection.
        /// Any part already in that slot is destroyed. Any part fits any slot; fails if the body's
        /// energy budget would be exceeded.
        /// </summary>
        public bool TryEquip(UnitBlueprint bp, string slotId, string partId, ContentDatabase db, out string error)
        {
            error = null;
            if (bp == null) { error = "No blueprint."; return false; }
            if (GetPartCount(partId) <= 0) { error = $"No copies of part '{partId}' left."; return false; }

            var body = db.GetBody(bp.bodyId);
            if (body == null) { error = $"Unknown body '{bp.bodyId}'."; return false; }
            var slot = body.GetSlot(slotId);
            if (slot == null) { error = $"Body '{body.displayName}' has no slot '{slotId}'."; return false; }
            var part = db.GetPart(partId);
            if (part == null) { error = $"Unknown part '{partId}'."; return false; }

            int energy = UnitAssembler.EnergyUsed(bp, db) + part.energyCost;
            var replaced = db.GetPart(bp.GetPartIn(slotId));
            if (replaced != null) energy -= replaced.energyCost;
            if (energy > body.energy) { error = $"Needs {energy} energy, body has {body.energy}."; return false; }

            RemovePart(partId);
            bp.SetPart(slotId, partId); // the replaced part is destroyed
            return true;
        }

        /// <summary>Removes a part from a blueprint. The part is destroyed, not returned.</summary>
        public void Unequip(UnitBlueprint bp, string slotId) => bp?.SetPart(slotId, null);

        // Save and load

        public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

        public static PlayerCollection FromJson(string json) =>
            string.IsNullOrEmpty(json) ? new PlayerCollection() : JsonUtility.FromJson<PlayerCollection>(json);
    }
}
