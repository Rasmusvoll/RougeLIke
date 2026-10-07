using UnityEngine;

namespace RougeLike.Units
{
    /// <summary>Base for every authored piece of content that saves refer to by id.</summary>
    public abstract class ContentDefinition : ScriptableObject
    {
        [Tooltip("Stable key used by saves and blueprints. Filled automatically; keep it fixed once content ships.")]
        public string id;
        public string displayName;
        public Sprite icon;
        public Rarity rarity;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString("N");
                UnityEditor.EditorUtility.SetDirty(this);
            }
            if (string.IsNullOrEmpty(displayName)) displayName = name;
        }
#endif
    }
}
