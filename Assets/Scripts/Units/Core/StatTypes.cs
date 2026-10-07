using System;

namespace RougeLike.Units
{
    public enum StatType { MaxHealth, Attack, Defense, Speed, Range }
    public enum SlotType { Head, Arm, Leg, Back, Tail }
    public enum ModifierOp { Flat, PercentAdd, PercentMult }
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
    public enum Team { Player, Enemy }

    [Serializable]
    public struct StatValue
    {
        public StatType stat;
        public float value;
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public ModifierOp op;
        public float value;
    }
}
