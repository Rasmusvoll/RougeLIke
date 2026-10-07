using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    /// <summary>Resolves content ids from saves and blueprints back to their definitions.</summary>
    [CreateAssetMenu(menuName = "Units/Content Database")]
    public class ContentDatabase : ScriptableObject
    {
        public List<BodyDefinition> bodies = new();
        public List<PartDefinition> parts = new();
        public List<BuffDefinition> buffs = new();

        Dictionary<string, BodyDefinition> bodyLookup;
        Dictionary<string, PartDefinition> partLookup;
        Dictionary<string, BuffDefinition> buffLookup;

        void OnEnable() => Rebuild();

        public void Rebuild()
        {
            bodyLookup = BuildLookup(bodies);
            partLookup = BuildLookup(parts);
            buffLookup = BuildLookup(buffs);
        }

        public BodyDefinition GetBody(string id) => Find(ref bodyLookup, bodies, id);
        public PartDefinition GetPart(string id) => Find(ref partLookup, parts, id);
        public BuffDefinition GetBuff(string id) => Find(ref buffLookup, buffs, id);

        static T Find<T>(ref Dictionary<string, T> lookup, List<T> list, string id) where T : ContentDefinition
        {
            if (string.IsNullOrEmpty(id)) return null;
            lookup ??= BuildLookup(list);
            return lookup.TryGetValue(id, out var d) ? d : null;
        }

        static Dictionary<string, T> BuildLookup<T>(List<T> list) where T : ContentDefinition
        {
            var dict = new Dictionary<string, T>();
            if (list == null) return dict;
            foreach (var d in list)
            {
                if (d == null || string.IsNullOrEmpty(d.id)) continue;
                if (!dict.TryAdd(d.id, d))
                    Debug.LogWarning($"ContentDatabase: duplicate id '{d.id}' on {d.name}");
            }
            return dict;
        }

#if UNITY_EDITOR
        void OnValidate() => Rebuild();
#endif
    }
}
