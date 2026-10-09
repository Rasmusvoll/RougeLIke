using System;
using System.Collections.Generic;
using RougeLike.Units;
using UnityEngine;

namespace RougeLike.Battle
{
    [Serializable]
    public class EnemyWaveEntry
    {
        public UnitBlueprint blueprint = new();
        [Tooltip("Spawn point on the enemy half: x across the field, y as depth (positive, away from the player).")]
        public Vector2 position;
    }

    /// <summary>A hand-made enemy line-up. Enemies use the same blueprints as player units.</summary>
    [CreateAssetMenu(menuName = "Battle/Enemy Wave")]
    public class EnemyWave : ScriptableObject
    {
        public string displayName;
        public List<EnemyWaveEntry> units = new();
        [Tooltip("Buffs every enemy in this wave gets, for tuning later waves.")]
        public List<BuffDefinition> buffs = new();
        [Tooltip("Where this wave is fought the first time it comes up. Empty uses the arena rotation.")]
        public ArenaDefinition arena;
    }
}
