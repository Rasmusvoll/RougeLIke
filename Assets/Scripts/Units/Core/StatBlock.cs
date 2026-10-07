using System;
using System.Collections.Generic;

namespace RougeLike.Units
{
    /// <summary>
    /// Final stats for one unit: (base + flat) * (1 + sum of PercentAdd) * product of (1 + PercentMult).
    /// Speed never goes below zero, and a fixed stat (e.g. speed 0 for a unit with no legs) ignores modifiers.
    /// </summary>
    public class StatBlock
    {
        readonly Dictionary<StatType, float> baseValues = new();
        readonly Dictionary<StatType, float> fixedValues = new();
        readonly List<StatModifier> modifiers = new();

        public StatBlock(IEnumerable<StatValue> baseStats)
        {
            if (baseStats == null) return;
            foreach (var s in baseStats)
                baseValues[s.stat] = GetBase(s.stat) + s.value;
        }

        public IReadOnlyList<StatModifier> Modifiers => modifiers;

        public void Add(StatModifier m) => modifiers.Add(m);

        public void AddRange(IEnumerable<StatModifier> ms)
        {
            if (ms != null) modifiers.AddRange(ms);
        }

        public void SetBase(StatType s, float value) => baseValues[s] = value;

        /// <summary>Pins a stat to a value whatever the modifiers say.</summary>
        public void Fix(StatType s, float value) => fixedValues[s] = value;

        public bool IsFixed(StatType s) => fixedValues.ContainsKey(s);

        public float GetBase(StatType s) =>
            fixedValues.TryGetValue(s, out var f) ? f : baseValues.TryGetValue(s, out var v) ? v : 0f;

        public float Get(StatType s)
        {
            if (fixedValues.TryGetValue(s, out var fixedValue)) return fixedValue;
            float flat = 0f, percentAdd = 0f, percentMult = 1f;
            foreach (var m in modifiers)
            {
                if (m.stat != s) continue;
                switch (m.op)
                {
                    case ModifierOp.Flat: flat += m.value; break;
                    case ModifierOp.PercentAdd: percentAdd += m.value; break;
                    case ModifierOp.PercentMult: percentMult *= 1f + m.value; break;
                }
            }
            float value = (GetBase(s) + flat) * (1f + percentAdd) * percentMult;
            return s == StatType.Speed ? Math.Max(0f, value) : value;
        }
    }
}
