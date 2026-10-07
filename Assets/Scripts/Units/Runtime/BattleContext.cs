using System.Collections.Generic;

namespace RougeLike.Units
{
    /// <summary>What an ability can see during a battle. Grows with the battle system.</summary>
    public class BattleContext
    {
        public readonly List<UnitInstance> Units = new();
        public readonly System.Random Rng;

        public BattleContext(int seed) => Rng = new System.Random(seed);

        public IEnumerable<UnitInstance> Enemies(Team of)
        {
            foreach (var u in Units)
                if (u != null && u.Team != of && u.IsAlive) yield return u;
        }

        public IEnumerable<UnitInstance> Allies(Team of)
        {
            foreach (var u in Units)
                if (u != null && u.Team == of && u.IsAlive) yield return u;
        }
    }
}
