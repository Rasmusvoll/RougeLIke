using System;
using UnityEngine;

namespace RougeLike.EditorTools
{
    /// <summary>
    /// A tiny 2D painter for the UI art: shapes are signed distance functions in a -1..1 square
    /// (y up), painted back to front with antialiased edges. Used to draw buff emblems, stat icons
    /// and the button and panel frames, so they can be regenerated whenever the palette changes.
    /// </summary>
    public class SdfCanvas
    {
        public readonly int width, height;
        readonly Color[] pixels;

        public SdfCanvas(int width, int height)
        {
            this.width = width;
            this.height = height;
            pixels = new Color[width * height];
        }

        /// <summary>Size of one pixel in shape units (shapes span 2 units across the shorter side).</summary>
        public float Pixel => 2f / Mathf.Min(width, height);

        /// <summary>Paints colour where the shape's distance is below zero, optionally shaded per point.</summary>
        public void Fill(Func<Vector2, float> shape, Color color, Func<Vector2, Color, Color> shade = null)
        {
            float px = Pixel;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = ToShape(x, y);
                float coverage = Mathf.Clamp01(0.5f - shape(p) / px);
                if (coverage <= 0f) continue;
                var c = shade != null ? shade(p, color) : color;
                Blend(x, y, c, coverage * c.a);
            }
        }

        /// <summary>Fills the shape grown by an ink border, then the shape itself on top.</summary>
        public void Inked(Func<Vector2, float> shape, Color fill, Color ink, float inkWidth)
        {
            Fill(p => shape(p) - inkWidth, ink);
            Fill(shape, fill);
        }

        public Vector2 ToShape(int x, int y)
        {
            float s = Mathf.Min(width, height) * 0.5f;
            return new Vector2((x + 0.5f - width * 0.5f) / s, (y + 0.5f - height * 0.5f) / s);
        }

        void Blend(int x, int y, Color c, float a)
        {
            ref var dst = ref pixels[y * width + x];
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return;
            var rgb = ((Vector4)c * a + (Vector4)dst * dst.a * (1f - a)) / outA;
            dst = new Color(rgb.x, rgb.y, rgb.z, outA);
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Shapes. All return signed distance: negative inside.

        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        public static float Ring(Vector2 p, Vector2 c, float r, float thickness) =>
            Mathf.Abs((p - c).magnitude - r) - thickness * 0.5f;

        public static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a;
            var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        public static float RoundedBox(Vector2 p, Vector2 center, Vector2 halfSize, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - halfSize + Vector2.one * radius;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>Exact distance to any simple polygon (Inigo Quilez's sdPolygon).</summary>
        public static float Polygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        public static float Union(params float[] d)
        {
            float m = float.MaxValue;
            foreach (var x in d) m = Mathf.Min(m, x);
            return m;
        }

        public static float Hash(Vector2 p) => Mathf.Repeat(Mathf.Sin(Vector2.Dot(p, new Vector2(127.1f, 311.7f))) * 43758.5453f, 1f);

        /// <summary>Smooth value noise in 0..1.</summary>
        public static float Noise(Vector2 p)
        {
            var i = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
            var f = p - i;
            f = new Vector2(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y));
            float a = Hash(i), b = Hash(i + Vector2.right), c = Hash(i + Vector2.up), d = Hash(i + Vector2.one);
            return Mathf.Lerp(Mathf.Lerp(a, b, f.x), Mathf.Lerp(c, d, f.x), f.y);
        }
    }
}
