using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    /// <summary>A unit assembled for one battle. Built by UnitAssembler; never saved.</summary>
    public class UnitInstance : MonoBehaviour
    {
        public UnitBlueprint Source { get; private set; }
        public StatBlock Stats { get; private set; }
        public float CurrentHealth;
        public List<AbilityDefinition> Abilities = new();
        public List<string> Tags = new();
        public Team Team;
        public Gait Gait { get; private set; }

        public bool IsAlive => CurrentHealth > 0f;
        /// <summary>Has a part that attacks (melee, ranged or thrower). Without one it can at most kick.</summary>
        public bool HasAttackPart => UnitAssembler.HasAttackPart(Tags);

        public void Initialize(UnitBlueprint source, StatBlock stats, List<AbilityDefinition> abilities, List<string> tags, Team team, Gait gait)
        {
            Gait = gait;
            Source = source;
            Stats = stats;
            Abilities = abilities;
            Tags = tags;
            Team = team;
            CurrentHealth = stats.Get(StatType.MaxHealth);
        }

        public void TakeDamage(float amount)
        {
            // Always at least 1 so heavy armour can't stall a battle forever.
            float dmg = Mathf.Max(1f, amount - Stats.Get(StatType.Defense));
            CurrentHealth = Mathf.Max(0f, CurrentHealth - dmg);
        }

        public void Heal(float amount) =>
            CurrentHealth = Mathf.Min(Stats.Get(StatType.MaxHealth), CurrentHealth + amount);
    }
}
