using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    /// <summary>Turns a UnitBlueprint into a UnitInstance. Used for both player and enemy units.</summary>
    public static class UnitAssembler
    {
        public static int EnergyUsed(UnitBlueprint bp, ContentDatabase db)
        {
            int total = 0;
            if (bp?.parts == null) return 0;
            foreach (var a in bp.parts)
            {
                var part = db.GetPart(a.partId);
                if (part != null) total += part.energyCost;
            }
            return total;
        }

        public static bool Validate(UnitBlueprint bp, ContentDatabase db, out string error)
        {
            error = null;
            if (bp == null) { error = "No blueprint."; return false; }
            var body = db.GetBody(bp.bodyId);
            if (body == null) { error = $"Unknown body '{bp.bodyId}'."; return false; }

            var used = new HashSet<string>();
            int energy = 0;
            foreach (var a in bp.parts)
            {
                var slot = body.GetSlot(a.slotId);
                if (slot == null) { error = $"Body '{body.displayName}' has no slot '{a.slotId}'."; return false; }
                if (!used.Add(a.slotId)) { error = $"Slot '{a.slotId}' is used twice."; return false; }
                var part = db.GetPart(a.partId);
                if (part == null) { error = $"Unknown part '{a.partId}'."; return false; }
                if (part.fitsSlot != slot.type) { error = $"{part.displayName} fits {part.fitsSlot}, not {slot.type}."; return false; }
                energy += part.energyCost;
            }
            if (energy > body.energy) { error = $"Parts cost {energy} energy, body has {body.energy}."; return false; }
            return true;
        }

        public static UnitInstance Build(UnitBlueprint bp, IEnumerable<BuffDefinition> buffs,
                                         ContentDatabase db, Transform parent, Team team = Team.Player)
        {
            // 1. Reject bad blueprints.
            if (!Validate(bp, db, out var error))
            {
                Debug.LogError($"Can't build unit '{bp?.name}': {error}");
                return null;
            }
            var body = db.GetBody(bp.bodyId);

            // 2. Spawn the body, then each part at its slot's attach point.
            var root = SpawnVisual(bp, db, parent);

            // 3. Gather modifiers from parts, then from run buffs that apply to this unit.
            //    Step 4 (the stat formula) runs on read in StatBlock.Get.
            var stats = ComputeStats(bp, buffs, db);
            var tags = CollectTags(bp, db);

            // 5. Collect innate and part abilities.
            var abilities = new List<AbilityDefinition>();
            foreach (var ab in body.innateAbilities) if (ab != null) abilities.Add(ab);
            foreach (var part in ResolveParts(bp, db))
                foreach (var ab in part.abilities) if (ab != null) abilities.Add(ab);

            if (!root.TryGetComponent<UnitInstance>(out var unit)) unit = root.AddComponent<UnitInstance>();
            unit.Initialize(bp, stats, abilities, tags, team);
            return unit;
        }

        /// <summary>
        /// Spawns the body model with each part at its slot's attach point, plus a UnitAnimator to
        /// move them, without any gameplay components. Used by Build and by the unit builder's
        /// preview. Skips parts in unknown slots.
        /// </summary>
        public static GameObject SpawnVisual(UnitBlueprint bp, ContentDatabase db, Transform parent)
        {
            var body = db.GetBody(bp?.bodyId);
            if (body == null) return null;

            GameObject root;
            if (body.prefab != null)
            {
                root = Object.Instantiate(body.prefab, parent);
            }
            else
            {
                root = new GameObject();
                root.transform.SetParent(parent, false);
            }
            root.name = string.IsNullOrEmpty(bp.name) ? body.displayName : bp.name;
            var animator = root.AddComponent<UnitAnimator>();

            foreach (var a in bp.parts)
            {
                var part = db.GetPart(a.partId);
                var slot = body.GetSlot(a.slotId);
                if (part == null || slot == null || part.prefab == null) continue;
                var go = Object.Instantiate(part.prefab, root.transform);
                go.transform.localPosition = slot.localPosition;
                if (slot.mirror) go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(-1f, 1f, 1f));
                go.name = $"{a.slotId}: {part.displayName}";
                animator.AddLimb(go.transform, a.slotId, slot.type, slot.mirror, part.tags, slot.localPosition);
            }
            return root;
        }

        /// <summary>Final stats for a blueprint: body base, part modifiers, then run buffs that apply.</summary>
        public static StatBlock ComputeStats(UnitBlueprint bp, IEnumerable<BuffDefinition> buffs, ContentDatabase db)
        {
            var body = db.GetBody(bp?.bodyId);
            var stats = new StatBlock(body != null ? body.baseStats : null);
            if (body == null) return stats;
            foreach (var part in ResolveParts(bp, db)) stats.AddRange(part.modifiers);
            if (buffs != null)
            {
                var tags = CollectTags(bp, db);
                foreach (var buff in buffs)
                    if (buff != null && (string.IsNullOrEmpty(buff.requiredTag) || tags.Contains(buff.requiredTag)))
                        stats.AddRange(buff.modifiers);
            }
            return stats;
        }

        public static List<string> CollectTags(UnitBlueprint bp, ContentDatabase db)
        {
            var tags = new List<string>();
            foreach (var part in ResolveParts(bp, db))
                foreach (var t in part.tags)
                    if (!tags.Contains(t)) tags.Add(t);
            return tags;
        }

        static List<PartDefinition> ResolveParts(UnitBlueprint bp, ContentDatabase db)
        {
            var list = new List<PartDefinition>();
            if (bp?.parts == null) return list;
            foreach (var a in bp.parts)
            {
                var part = db.GetPart(a.partId);
                if (part != null) list.Add(part);
            }
            return list;
        }

        /// <summary>Resolves a collection's run buff ids through the database.</summary>
        public static List<BuffDefinition> ResolveBuffs(IEnumerable<string> buffIds, ContentDatabase db)
        {
            var list = new List<BuffDefinition>();
            if (buffIds == null) return list;
            foreach (var id in buffIds)
            {
                var b = db.GetBuff(id);
                if (b != null) list.Add(b);
            }
            return list;
        }
    }
}
