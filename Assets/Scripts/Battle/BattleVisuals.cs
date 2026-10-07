using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>Shared runtime materials for the procedural battle visuals, cached per look.</summary>
    public static class BattleVisuals
    {
        /// <summary>Palette colours from ArtSource/STYLE.md that the battle code paints with.</summary>
        public static class Palette
        {
            public static readonly Color Ink = Hex("2B1F1A");
            public static readonly Color Parchment = Hex("EAD9B0");
            public static readonly Color Forest = Hex("4F6B3A");
            public static readonly Color Stone = Hex("8C8577");
            public static readonly Color Acid = Hex("A8C23A");
            public static readonly Color EyrieBlue = Hex("3F6E94");
            public static readonly Color MarquiseOrange = Hex("D9792B");

            static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.magenta;
        }

        public const float DefaultOutline = 3f;

        static readonly Dictionary<Color, Material> unlit = new();
        static readonly Dictionary<(Color, float, float), Material> toon = new();
        static Shader toonShader;

        public static Material Unlit(Color c)
        {
            if (unlit.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Shader.Find("Unlit/Color")) { color = c };
            unlit[c] = m;
            return m;
        }

        /// <summary>The storybook toon look with an ink outline.</summary>
        public static Material Lit(Color c) => Toon(c, DefaultOutline);

        /// <summary>Toon material; outline in pixels (0 for flat overlays), grain for big flat ground.</summary>
        public static Material Toon(Color c, float outline, float grain = 0f)
        {
            var key = (c, outline, grain);
            if (toon.TryGetValue(key, out var m) && m != null) return m;
            if (toonShader == null) toonShader = Shader.Find("RougeLike/Toon") ?? Shader.Find("Standard");
            m = new Material(toonShader) { color = c };
            m.SetFloat("_OutlineWidth", outline);
            m.SetFloat("_GrainStrength", grain);
            m.SetFloat("_SmoothNormals", 0f); // Unity primitives carry no smoothed normals
            toon[key] = m;
            return m;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}
