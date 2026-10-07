using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>Shared runtime materials for the placeholder battle visuals, one per colour.</summary>
    public static class BattleVisuals
    {
        static readonly Dictionary<Color, Material> unlit = new();
        static readonly Dictionary<Color, Material> lit = new();

        public static Material Unlit(Color c) => Get(unlit, c, "Unlit/Color");

        public static Material Lit(Color c) => Get(lit, c, "Standard");

        static Material Get(Dictionary<Color, Material> cache, Color c, string shader)
        {
            if (cache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Shader.Find(shader)) { color = c };
            if (shader == "Standard") m.SetFloat("_Glossiness", 0.1f);
            cache[c] = m;
            return m;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}
