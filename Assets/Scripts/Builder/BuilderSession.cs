using System;
using System.Collections.Generic;
using RougeLike.Units;

namespace RougeLike.Builder
{
    /// <summary>
    /// The unit builder's rules, kept free of UI so they can be tested and reused.
    /// Edits the player's saved blueprints in place; parts follow the collection's consumable rules.
    /// </summary>
    public class BuilderSession
    {
        public readonly ContentDatabase Db;
        public readonly PlayerCollection Collection;

        public event Action Changed;

        public int SelectedIndex { get; private set; } = -1;
        public string SelectedSlotId { get; private set; }
        /// <summary>Last rule violation, shown to the player. Cleared by the next successful action.</summary>
        public string Message { get; private set; }

        public BuilderSession(ContentDatabase db, PlayerCollection collection)
        {
            Db = db;
            Collection = collection;
            if (Collection.blueprints.Count > 0) SelectedIndex = 0;
        }

        public UnitBlueprint Current =>
            SelectedIndex >= 0 && SelectedIndex < Collection.blueprints.Count ? Collection.blueprints[SelectedIndex] : null;

        public BodyDefinition CurrentBody => Db.GetBody(Current?.bodyId);

        public int EnergyUsed => UnitAssembler.EnergyUsed(Current, Db);

        public int EnergyMax => CurrentBody != null ? CurrentBody.energy : 0;

        public List<BuffDefinition> RunBuffs => UnitAssembler.ResolveBuffs(Collection.runBuffIds, Db);

        public StatBlock CurrentStats => UnitAssembler.ComputeStats(Current, RunBuffs, Db);

        public IEnumerable<BodyDefinition> OwnedBodies
        {
            get
            {
                foreach (var id in Collection.bodyIds)
                {
                    var b = Db.GetBody(id);
                    if (b != null) yield return b;
                }
            }
        }

        public Gait CurrentGait => Gait.Of(Current, Db);

        /// <summary>Unequipped parts in the collection. Any of them fits any slot.</summary>
        public IEnumerable<(PartDefinition part, int count)> Inventory()
        {
            foreach (var stack in Collection.parts)
            {
                var p = Db.GetPart(stack.partId);
                if (p == null || stack.count <= 0) continue;
                yield return (p, stack.count);
            }
        }

        // Units

        public void Select(int index)
        {
            if (index < 0 || index >= Collection.blueprints.Count) return;
            SelectedIndex = index;
            SelectedSlotId = null;
            Ok();
        }

        public void NewUnit(string bodyId)
        {
            var body = Db.GetBody(bodyId);
            if (body == null || !Collection.OwnsBody(bodyId)) { Fail("You don't own that body."); return; }
            Collection.blueprints.Add(new UnitBlueprint { name = NextName(body.displayName), bodyId = bodyId });
            SelectedIndex = Collection.blueprints.Count - 1;
            SelectedSlotId = null;
            Ok();
        }

        /// <summary>Deletes the selected unit. Its equipped parts are destroyed.</summary>
        public void DeleteCurrent()
        {
            if (Current == null) return;
            Collection.blueprints.RemoveAt(SelectedIndex);
            SelectedIndex = Math.Min(SelectedIndex, Collection.blueprints.Count - 1);
            SelectedSlotId = null;
            Ok();
        }

        public void Rename(string name)
        {
            if (Current == null || string.IsNullOrWhiteSpace(name)) return;
            Current.name = name.Trim();
            Ok();
        }

        /// <summary>Puts the selected unit on a different body. Equipped parts are destroyed.</summary>
        public void ChangeBody(string bodyId)
        {
            if (Current == null || Current.bodyId == bodyId) return;
            if (!Collection.OwnsBody(bodyId) || Db.GetBody(bodyId) == null) { Fail("You don't own that body."); return; }
            Current.parts.Clear();
            Current.bodyId = bodyId;
            SelectedSlotId = null;
            Ok();
        }

        // Slots

        public void SelectSlot(string slotId)
        {
            SelectedSlotId = CurrentBody?.GetSlot(slotId) != null ? slotId : null;
            Ok();
        }

        /// <summary>Energy the unit would use if this part went into the slot (replacing what's there).</summary>
        public int EnergyIfEquipped(string slotId, PartDefinition part)
        {
            int e = EnergyUsed + part.energyCost;
            var replaced = Db.GetPart(Current?.GetPartIn(slotId));
            if (replaced != null) e -= replaced.energyCost;
            return e;
        }

        public bool Equip(string slotId, string partId)
        {
            if (!Collection.TryEquip(Current, slotId, partId, Db, out var error)) { Fail(error); return false; }
            Ok();
            return true;
        }

        /// <summary>Removes a part from the selected unit. The part is destroyed.</summary>
        public void Scrap(string slotId)
        {
            if (Current?.GetPartIn(slotId) == null) return;
            Collection.Unequip(Current, slotId);
            Ok();
        }

        string NextName(string baseName)
        {
            for (int i = 1; ; i++)
            {
                var n = $"{baseName} {i}";
                if (!Collection.blueprints.Exists(b => b.name == n)) return n;
            }
        }

        void Ok()
        {
            Message = null;
            Changed?.Invoke();
        }

        void Fail(string msg)
        {
            Message = msg;
            Changed?.Invoke();
        }
    }
}
