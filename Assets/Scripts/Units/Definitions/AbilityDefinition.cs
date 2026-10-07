using UnityEngine;

namespace RougeLike.Units
{
    /// <summary>
    /// Subclass per behaviour (MeleeStrike, Heal, Projectile...). New behaviours need code; tuning lives in assets.
    /// </summary>
    public abstract class AbilityDefinition : ScriptableObject
    {
        public string id;
        public float cooldown = 1f;

        public abstract void Execute(UnitInstance user, BattleContext ctx);

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
        }
#endif
    }
}
