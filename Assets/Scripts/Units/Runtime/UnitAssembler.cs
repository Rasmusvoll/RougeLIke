using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    /// <summary>Turns a UnitBlueprint into a UnitInstance. Used for both player and enemy units.</summary>
    public static class UnitAssembler
    {
        static readonly string[] AttackTags = { "melee", "ranged", "thrower" };

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

        /// <summary>Any part fits any slot; the body's energy budget is the limit.</summary>
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
            unit.Initialize(bp, stats, abilities, tags, team, Gait.Of(bp, db));
            return unit;
        }

        /// <summary>
        /// Spawns the body model with each part at its slot's attach point, plus a UnitAnimator to
        /// move them, without any gameplay components. Used by Build and by the unit builder's
        /// preview. Skips parts in unknown slots.
        /// Each part is turned to face out of its slot (see MountRotation) and mirrored on the left.
        /// Then the unit is stood on the ground: its lowest point goes to y = 0, and legs too short
        /// to reach the ground from where they're mounted are stretched until they do.
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
            float ground = LowestPoint(root.transform, root.transform);

            var mounted = new List<(GameObject go, SlotAssignment a, PartDefinition part, SlotDefinition slot, bool walks)>();
            foreach (var a in bp.parts)
            {
                var part = db.GetPart(a.partId);
                var slot = body.GetSlot(a.slotId);
                if (part == null || slot == null || part.prefab == null) continue;
                var go = Object.Instantiate(part.prefab, root.transform);
                go.transform.localPosition = slot.localPosition;
                var rot = MountRotation(part.kind, slot);
                var scale = go.transform.localScale;
                if (slot.Mirrored)
                {
                    // Mirror across the body's X: reflect the rotation and flip the part itself.
                    rot = new Quaternion(rot.x, -rot.y, -rot.z, rot.w);
                    scale.x = -scale.x;
                }
                go.transform.localRotation = rot * go.transform.localRotation;
                go.transform.localScale = scale;
                go.name = $"{a.slotId}: {part.displayName}";
                bool walks = part.IsLocomotion && Gait.Reaches(slot);
                mounted.Add((go, a, part, slot, walks));
                ground = Mathf.Min(ground, LowestPoint(go.transform, root.transform));
            }

            // Stretch walking legs down to the ground, then stand the whole unit on it.
            foreach (var m in mounted)
            {
                if (!m.walks) continue;
                float foot = LowestPoint(m.go.transform, root.transform);
                float hip = m.slot.localPosition.y;
                if (foot <= ground + 0.01f || hip - foot < 0.05f) continue;
                float k = Mathf.Clamp((hip - ground) / (hip - foot), 1f, 4f);
                var s = m.go.transform.localScale;
                m.go.transform.localScale = new Vector3(s.x, s.y * k, s.z);
            }
            root.transform.localPosition += root.transform.localRotation * new Vector3(0f, -ground * root.transform.localScale.y, 0f);

            var animator = root.AddComponent<UnitAnimator>();
            foreach (var m in mounted)
            {
                var attach = m.slot.localPosition;
                attach.y -= ground;
                animator.AddLimb(m.go.transform, m.a.slotId, m.part.kind, m.slot.Mirrored, m.part.tags, attach, m.walks);
            }
            return root;
        }

        /// <summary>
        /// Which way a part faces on a slot, for the right-hand side (left slots mirror it). Parts are
        /// modelled for their own slot type, so a part on its own type of slot isn't turned. Otherwise
        /// it's turned to point out of the slot: a tail on the head becomes a lance, a shell on the
        /// side a shield, an arm on the back reaches up. Heads keep looking forward unless mounted at
        /// the rear, and legs always hang down, splayed out from the slot (or wave in the air on top).
        /// </summary>
        public static Quaternion MountRotation(SlotType kind, SlotDefinition slot)
        {
            var at = slot.type;
            if (kind == at) return Quaternion.identity;
            switch (kind)
            {
                case SlotType.Head:
                    return at == SlotType.Tail ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                case SlotType.Leg:
                    return at switch
                    {
                        SlotType.Head => Quaternion.Euler(0f, -90f, 0f),  // splay forward
                        SlotType.Tail => Quaternion.Euler(0f, 90f, 0f),   // splay back
                        SlotType.Back => Quaternion.Euler(0f, 0f, 180f),  // feet in the air
                        _ => Quaternion.identity,
                    };
                case SlotType.Arm:
                    return at switch
                    {
                        SlotType.Tail => Quaternion.Euler(0f, 180f, 0f),
                        SlotType.Back => Quaternion.Euler(0f, 0f, 70f),   // reach up
                        _ => Quaternion.identity,                         // claws already reach forward
                    };
            }
            var from = Axis(kind);
            var to = Axis(at);
            if (Vector3.Dot(from, to) < -0.99f) return Quaternion.AngleAxis(180f, Vector3.up);
            return Quaternion.FromToRotation(from, to);
        }

        /// <summary>The way a part of this kind points out of the body in its model, right-hand side.</summary>
        static Vector3 Axis(SlotType kind) => kind switch
        {
            SlotType.Head => Vector3.forward,
            SlotType.Tail => Vector3.back,
            SlotType.Back => Vector3.up,
            _ => Vector3.right,
        };

        /// <summary>Lowest point of an object's meshes, in the unit root's local space.</summary>
        static float LowestPoint(Transform t, Transform root)
        {
            float min = float.MaxValue;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh = null;
                if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf)) mesh = mf.sharedMesh;
                else if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                if (mesh == null) continue;
                var b = mesh.bounds;
                var toRoot = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    min = Mathf.Min(min, toRoot.MultiplyPoint3x4(c).y);
                }
            }
            return min == float.MaxValue ? 0f : min;
        }

        /// <summary>
        /// Final stats for a blueprint: body base, part modifiers, then run buffs that apply. Speed's
        /// base comes from the unit's legs (see Gait); with none it's fixed at zero.
        /// </summary>
        public static StatBlock ComputeStats(UnitBlueprint bp, IEnumerable<BuffDefinition> buffs, ContentDatabase db)
        {
            var body = db.GetBody(bp?.bodyId);
            var stats = new StatBlock(body != null ? body.baseStats : null);
            if (body == null) return stats;
            var gait = Gait.Of(bp, db);
            stats.SetBase(StatType.Speed, gait.Speed);
            if (!gait.CanMove) stats.Fix(StatType.Speed, 0f);
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

        public static bool HasAttackPart(IList<string> tags)
        {
            if (tags == null) return false;
            foreach (var t in AttackTags) if (tags.Contains(t)) return true;
            return false;
        }

        /// <summary>How the unit fights, in a few words, e.g. "melee", "ranged" or "only kicks".</summary>
        public static string DescribeAttack(UnitBlueprint bp, ContentDatabase db)
        {
            var tags = CollectTags(bp, db);
            if (tags.Contains("thrower")) return "throws boulders";
            if (tags.Contains("ranged")) return "ranged";
            if (tags.Contains("melee")) return "melee";
            return Gait.Of(bp, db).CanMove ? "only kicks" : "no attack";
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
