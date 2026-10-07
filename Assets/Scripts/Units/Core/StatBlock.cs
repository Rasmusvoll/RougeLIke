using System.Collections.Generic;

namespace RougeLike.Units
{
    /// <summary>
    /// Final stats for one unit: (base + flat) * (1 + sum of PercentAdd) * product of (1 + PercentMult).
    /// </summary>
    public class StatBlock
    {
        readonly Dictionary<StatType, float> baseValues = new();
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

        public float GetBase(StatType s) => baseValues.TryGetValue(s, out var v) ? v : 0f;

        public float Get(StatType s)
        {
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
            return (GetBase(s) + flat) * (1f + percentAdd) * percentMult;
        }
    }
}
