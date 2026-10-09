using UnityEngine;

namespace RougeLike.Units
{
    /// <summary>
    /// How a unit gets around, worked out from its locomotion parts (anything with a stride, i.e.
    /// legs) and where they're mounted, plus any legs modelled into the body. No locomotion part means the unit can't move at all. More legs
    /// carry it faster and steadier; legs bunched on one side make it limp and pull to the other side.
    /// Legs on a top (Back) slot point up into the air, so they don't count.
    /// </summary>
    public readonly struct Gait
    {
        /// <summary>Legs that reach the ground.</summary>
        public readonly int Legs;
        /// <summary>Base ground speed in m/s, before part and buff modifiers.</summary>
        public readonly float Speed;
        /// <summary>-1 (all legs on the left) to 1 (all on the right).</summary>
        public readonly float Lean;
        /// <summary>How hard the unit is to knock over; 1 is a normal two-legged unit.</summary>
        public readonly float Stability;

        public bool CanMove => Legs > 0;
        public bool Limps => Legs > 0 && Mathf.Abs(Lean) > 0.3f;

        Gait(int legs, float speed, float lean, float stability)
        {
            Legs = legs;
            Speed = speed;
            Lean = lean;
            Stability = stability;
        }

        /// <summary>A part on this slot stands on the ground: anything but the top of the body.</summary>
        public static bool Reaches(SlotDefinition slot) => slot.type != SlotType.Back;

        public static Gait Of(UnitBlueprint bp, ContentDatabase db)
        {
            var body = db.GetBody(bp?.bodyId);
            if (body == null) return default;

            // Built-in legs stand in a pair under the body, so they don't lean either way.
            int legs = Mathf.Max(0, body.builtInLegs);
            float strideSum = legs * body.builtInStride, side = 0f;
            foreach (var a in bp.parts)
            {
                var part = db.GetPart(a.partId);
                var slot = body.GetSlot(a.slotId);
                if (part == null || slot == null || !part.IsLocomotion || !Reaches(slot)) continue;
                legs++;
                strideSum += part.stride;
                float x = slot.localPosition.x;
                side += x > 0.05f ? 1f : x < -0.05f ? -1f : 0f;
            }
            if (legs == 0) return new Gait(0, 0f, 0f, 1.5f); // sits planted on its belly

            float lean = side / legs;
            float limp = 1f - 0.3f * Mathf.Abs(lean);
            float speed = strideSum / legs * GaitFactor(legs) * Mathf.Max(0.1f, body.moveScale) * limp;
            return new Gait(legs, speed, lean, StabilityFactor(legs) * limp);
        }

        /// <summary>
        /// How well feet hold a body up, 0 to 1: more feet hold better, and feet bunched toward one
        /// end (offset from the body's middle, as a fraction of its half size) leave the other end
        /// hanging. Used for the belly drag in battle and the droop in the animation.
        /// </summary>
        public static float Support(int feet, Vector2 offset) =>
            feet <= 0 ? 0f : Mathf.Clamp01(0.35f * feet + 0.3f) * (1f - 0.7f * Mathf.Clamp01(offset.magnitude));

        /// <summary>One leg hops, two walk, more scuttle a bit faster.</summary>
        static float GaitFactor(int legs) => legs switch { 1 => 0.5f, 2 => 1f, 3 => 1.1f, 4 => 1.2f, _ => 1.25f };

        static float StabilityFactor(int legs) => legs switch { 1 => 0.6f, 2 => 1f, 3 => 1.15f, _ => 1.3f };

        /// <summary>A few words for the builder, e.g. "Walks on 4 legs" or "Can't move".</summary>
        public string Describe()
        {
            if (!CanMove) return "Can't move: add legs";
            if (Legs == 1) return "Hops on one leg";
            return Limps ? $"Limps on {Legs} legs" : $"Walks on {Legs} legs";
        }
    }
}
